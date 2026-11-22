using System.Net.Http.Json;
using System.Text.Json;
using Mobile.Client.Models;

namespace Mobile.Client.Services;

// ─────────────────────────────────────────────────────────────────────────────
// AUTENTICACIÓN CON EL BACKEND (reemplaza al servicio temporal en memoria)
//
// La app NUNCA envía correos ni guarda usuarios: solo le habla a la API.
// El backend es quien genera el código de 6 dígitos y lo manda al correo.
//
// Endpoints que debe tener la API (todos POST, bajo UrlBase):
//   registro          { nombre, cedula, correo, clave }            → crea usuario SIN verificar y envía código
//   verificar-correo  { correo, codigo }                           → marca el correo como verificado
//   reenviar-codigo   { correo }                                   → reenvía el código de verificación
//   login             { correo, clave }                            → devuelve Usuario; si el correo no está
//                                                                    verificado: 403 { mensaje, pendienteVerificar: true }
//   olvide-clave      { correo }                                   → envía código de recuperación
//   validar-codigo    { correo, codigo }                           → solo comprueba que el código sirva
//   restablecer-clave { correo, codigo, clave }                    → vuelve a validar el código y cambia la clave
//
// Cualquier error devuelve { mensaje: "texto para el usuario" } con un status distinto de 200.
//
// ═════════════════════ NOTAS PARA EL BACKEND (lo que debe mejorarse/cambiarse) ═════════════════════
//
// 1) CÓDIGOS DE 6 DÍGITOS (registro y recuperación)
//    • Generarlos con un generador criptográficamente seguro (RandomNumberGenerator), no con Random.
//    • Guardar solo el HASH del código, nunca el código en claro, junto con su fecha de expiración.
//    • Expiración corta (p. ej. 10 minutos) y de UN SOLO USO: se invalida al usarse o al pedir uno nuevo.
//    • Límite de intentos (p. ej. 5 fallos y el código se invalida). Sin esto, 1.000.000 de
//      combinaciones se pueden probar por fuerza bruta.
//    • Límite de reenvíos: espera mínima entre envíos y tope por hora (por correo y por IP).
//    • Si cambia la longitud del código, hay que ajustar LongitudCodigo en VerificarCodigo.xaml.cs
//      y las casillas de VerificarCodigo.xaml.
//
// 2) TOKEN DE RESTABLECIMIENTO (cambio más importante en recuperar clave)
//    • Hoy la app guarda el código ya validado (_codigoValidado) y lo reenvía en "restablecer-clave".
//    • Más sólido: "validar-codigo" devuelve un TOKEN de un solo uso y vida corta, y
//      "restablecer-clave" recibe { correo, token, clave } en lugar del código.
//    • Si se adopta, en la app solo cambian _codigoValidado y el cuerpo de RestablecerClaveAsync.
//
// 3) SESIÓN Y AUTENTICACIÓN
//    • "login" debe devolver un TOKEN (p. ej. JWT con expiración + token de renovación).
//    • La app hoy NO lo guarda ni lo envía: UsuarioActual vive solo en memoria y se pierde al cerrar.
//      Cuando existan endpoints protegidos (catálogo, carrito, historial) habrá que guardar el token
//      en SecureStorage y añadirlo a cada petición (cabecera Authorization: Bearer ...).
//    • Limitar intentos fallidos de login (bloqueo temporal) y no dar pistas de si el correo existe.
//    • "Cerrar sesión" debería invalidar el token en el servidor, no solo limpiar el estado local.
//
// 4) CONTRASEÑAS
//    • Guardarlas con un hash para contraseñas (Argon2id o bcrypt), nunca en claro.
//    • La app limita la clave a 6–10 caracteres; es un tope bajo. El backend debe validar su propia
//      política (p. ej. mínimo 8, sin tope tan corto) y la app deberá ajustarse a ella.
//
// 5) PRIVACIDAD DE RESPUESTAS
//    • "olvide-clave" y "reenviar-codigo" deben responder IGUAL exista o no el correo
//      (para no revelar qué correos están registrados).
//
// 6) VALIDACIÓN DOBLE
//    • Las validaciones de la app (cédula, correo, longitud de clave) son solo comodidad para el usuario.
//      El backend debe volver a validar TODO: nunca confiar en lo que llega del móvil.
//
// 7) TRANSPORTE Y DESPLIEGUE
//    • Producción siempre con https (certificado válido). Ajustar UrlBase abajo.
//    • Respuestas coherentes: 200 éxito; 4xx con { mensaje } para errores del usuario; 403 con
//      { mensaje, pendienteVerificar: true } si el correo no está verificado; 429 si hay demasiados intentos.
// ════════════════════════════════════════════════════════════════════════════════════════════════
// ─────────────────────────────────────────────────────────────────────────────

public class ServicioAutenticacion
{
    // Debe terminar en "/". En el emulador de Android, localhost del PC es http://10.0.2.2:PUERTO/api/auth/
    // (con http hay que permitir tráfico sin cifrar en Android; en producción usa https).
    // BACKEND: "TU-SERVIDOR" es un MARCADOR. Hay que reemplazarlo por la dirección real de la API
    // antes de probar o publicar. Ideal: moverla a una configuración (una por entorno: desarrollo/producción).
    private const string UrlBase = "https://TU-SERVIDOR/api/auth/";

    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(UrlBase),
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Usuario con la sesión abierta (null si nadie ha iniciado sesión).
    public Usuario? UsuarioActual { get; private set; }

    // ── Estado del flujo del código (lo comparten Registro/Olvido → Código → NuevaClave) ──
    // BACKEND: _codigoValidado es el código de 6 dígitos ya comprobado, que se reenvía al cambiar la clave.
    // Cuando el backend entregue un TOKEN de restablecimiento (nota 2), este campo pasará a guardar el token.
    public string CorreoPendiente { get; private set; } = "";
    public PropositoCodigo Proposito { get; private set; }
    private string _codigoValidado = "";

    public void CerrarSesion() => UsuarioActual = null;

    // ───────────────────────── Inicio de sesión y registro ─────────────────────────

    public async Task<Resultado> IniciarSesionAsync(string correo, string clave)
    {
        var (resultado, usuario) = await PostAsync("login", new { correo, clave }, leerUsuario: true);

        if (resultado.Exito)
            UsuarioActual = usuario;
        else if (resultado.PendienteVerificar)
            IniciarFlujo(correo, PropositoCodigo.VerificarCorreo);   // cuenta creada pero sin verificar

        return resultado;
    }

    public async Task<Resultado> RegistrarAsync(string nombre, string cedula, string correo, string clave)
    {
        var (resultado, _) = await PostAsync("registro", new { nombre, cedula, correo, clave });

        if (resultado.Exito)
            IniciarFlujo(correo, PropositoCodigo.VerificarCorreo);

        return resultado;
    }

    // ───────────────────────── Recuperar contraseña ─────────────────────────

    public async Task<Resultado> SolicitarRecuperacionAsync(string correo)
    {
        var (resultado, _) = await PostAsync("olvide-clave", new { correo });

        if (resultado.Exito)
            IniciarFlujo(correo, PropositoCodigo.RecuperarClave);

        return resultado;
    }

    public async Task<Resultado> RestablecerClaveAsync(string clave)
    {
        var (resultado, _) = await PostAsync("restablecer-clave",
            new { correo = CorreoPendiente, codigo = _codigoValidado, clave });

        if (resultado.Exito)
            IniciarFlujo("", PropositoCodigo.RecuperarClave);        // limpia el flujo

        return resultado;
    }

    // ───────────────────────── Pantalla del código (sirve para los dos flujos) ─────────────────────────

    public async Task<Resultado> VerificarCodigoAsync(string codigo)
    {
        string ruta = Proposito == PropositoCodigo.VerificarCorreo ? "verificar-correo" : "validar-codigo";
        var (resultado, _) = await PostAsync(ruta, new { correo = CorreoPendiente, codigo });

        if (resultado.Exito && Proposito == PropositoCodigo.RecuperarClave)
            _codigoValidado = codigo;            // se reenvía al cambiar la clave

        return resultado;
    }

    public async Task<Resultado> ReenviarCodigoAsync()
    {
        string ruta = Proposito == PropositoCodigo.VerificarCorreo ? "reenviar-codigo" : "olvide-clave";
        var (resultado, _) = await PostAsync(ruta, new { correo = CorreoPendiente });
        return resultado;
    }

    // ───────────────────────── Utilidades ─────────────────────────

    private void IniciarFlujo(string correo, PropositoCodigo proposito)
    {
        CorreoPendiente = correo;
        Proposito = proposito;
        _codigoValidado = "";
    }

    // Único lugar que habla con la red: manda el JSON, lee la respuesta y traduce errores a texto.
    // BACKEND/FUTURO: aquí es donde se agregará la cabecera "Authorization: Bearer <token>" a cada petición
    // cuando el login devuelva un token (nota 3). Como todas las llamadas pasan por este método, el cambio es un solo punto.
    private async Task<(Resultado Resultado, Usuario? Usuario)> PostAsync(string ruta, object datos, bool leerUsuario = false)
    {
        try
        {
            using var respuesta = await _http.PostAsJsonAsync(ruta, datos);

            if (respuesta.IsSuccessStatusCode)
            {
                var usuario = leerUsuario ? await respuesta.Content.ReadFromJsonAsync<Usuario>() : null;
                return (new Resultado(true, "OK"), usuario);
            }

            ErrorApi? error = null;
            try { error = await respuesta.Content.ReadFromJsonAsync<ErrorApi>(); }
            catch (JsonException) { /* la API no devolvió JSON: se usa el mensaje genérico */ }

            return (new Resultado(false, error?.Mensaje ?? "Ocurrió un error. Inténtalo de nuevo.",
                                  error?.PendienteVerificar ?? false), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return (new Resultado(false, "No se pudo conectar con el servidor. Revisa tu conexión."), null);
        }
    }
}
