using Mobile.Client.Services;

namespace Mobile.App.Views;

public partial class IniciarSesion : ContentPage
{
    // Colores del enlace: azul normal y azul más oscuro mientras reacciona al toque.
    private static readonly Color AzulNormal = Color.FromArgb("#1A73E8");
    private static readonly Color AzulPresionado = Color.FromArgb("#174EA6");

    public IniciarSesion()
    {
        InitializeComponent();

        // Los botones se conectan por código (no en el XAML).
        BotonIngresar.Clicked += async (s, e) => await Ingresar();

        // "Registrarse" (arriba) lleva a la pantalla de registro.
        // "//Registro" es la ruta definida en AppShell.xaml.
        BotonIrRegistro.Clicked += async (s, e) => await Shell.Current.GoToAsync("//Registro");

        // "¿Olvidaste tu contraseña?" inicia la recuperación por código.
        TocarOlvido.Tapped += async (s, e) => await AbrirOlvido();

        // ── LOGO COMPACTO CON EL TECLADO ──────────────────────────────────────────
        // FUNCIÓN: con el teclado abierto se oculta el logo de 220 px para que quepan los dos
        // campos y el botón "Ingresar" sin tener que desplazar la pantalla.
        // CÓMO: mientras la pantalla está visible (Appearing → Disappearing) se escucha el evento
        // TecladoEstado.Cambio. Al salir de la pantalla se deja de escuchar (-=): Shell reutiliza
        // las páginas y, si no se desuscribe, se acumularían avisos repetidos.
        // Al aparecer se consulta el estado actual por si el teclado ya estaba abierto.
        Appearing += (s, e) =>
        {
            TecladoEstado.Cambio += AjustarPorTeclado;
            AjustarPorTeclado(TecladoEstado.Visible);
        };
        Disappearing += (s, e) => TecladoEstado.Cambio -= AjustarPorTeclado;
    }

    // Muestra el logo cuando el teclado está cerrado y lo oculta cuando está abierto.
    private void AjustarPorTeclado(bool tecladoVisible) => ImagenLogo.IsVisible = !tecladoVisible;

    // Al tocar el enlace reacciona (azul oscuro, subrayado y se atenúa) y luego abre la pantalla.
    // Al terminar vuelve a su estado normal, porque Shell reutiliza esta página.
    private async Task AbrirOlvido()
    {
        EnlaceOlvido.TextColor = AzulPresionado;
        EnlaceOlvido.TextDecorations = TextDecorations.Underline;
        await EnlaceOlvido.FadeToAsync(0.5, 90);

        await Shell.Current.GoToAsync("//OlvidoClave");

        EnlaceOlvido.TextColor = AzulNormal;
        EnlaceOlvido.TextDecorations = TextDecorations.None;
        EnlaceOlvido.Opacity = 1;
    }

    private async Task Ingresar()
    {
        string correo = CampoCorreo.Text?.Trim() ?? "";
        string clave = CampoClave.Text ?? "";

        // 1) Validación básica: no dejar campos vacíos.
        if (correo == "" || clave == "")
        {
            await DisplayAlertAsync("Faltan datos", "Escribe tu correo y tu contraseña.", "OK");
            return;
        }

        // Se desactiva el botón mientras se verifica, para evitar toques repetidos.
        BotonIngresar.IsEnabled = false;
        try
        {
            // 2) Se le pregunta al backend.
            // BACKEND: aquí debería devolver también un TOKEN de sesión (ver notas en ServicioAutenticacion)
            // y limitar los intentos fallidos de login (bloqueo temporal por correo/IP).
            var resultado = await Servicios.Autenticacion.IniciarSesionAsync(correo, clave);

            // 3) Cuenta creada pero con el correo sin verificar: se manda a escribir el código.
            if (resultado.PendienteVerificar)
            {
                await Servicios.Autenticacion.ReenviarCodigoAsync();   // código nuevo por si el anterior venció
                await Shell.Current.GoToAsync("//VerificarCodigo");
                return;
            }

            if (!resultado.Exito)
            {
                await DisplayAlertAsync("No se pudo ingresar", resultado.Mensaje, "OK");
                return;
            }

            // 4) Si todo salió bien: se limpia la contraseña y se navega al menú.
            CampoClave.Text = string.Empty;
            await Shell.Current.GoToAsync("//MenuPrincipal");
        }
        finally
        {
            BotonIngresar.IsEnabled = true;
        }
    }
}
