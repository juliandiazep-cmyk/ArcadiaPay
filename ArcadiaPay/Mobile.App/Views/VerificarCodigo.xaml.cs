using Mobile.Client.Models;
using Mobile.Client.Services;

namespace Mobile.App.Views;

// PASO 2: el usuario escribe el código de 6 dígitos que llegó a su correo.
// Esta misma pantalla sirve para dos flujos (lo indica Autenticacion.Proposito):
//   • VerificarCorreo → tras registrarse: si el código es correcto, va al login.
//   • RecuperarClave  → tras "¿Olvidaste tu contraseña?": si es correcto, va a NuevaClave.
public partial class VerificarCodigo : ContentPage
{
    // BACKEND: debe coincidir con la longitud del código que genere y envíe el backend.
    // Si el backend cambia a un código más largo o alfanumérico, hay que cambiar esto Y las casillas del XAML
    // (y el filtro "solo dígitos" de AlCambiarCodigo). El código debe expirar y tener límite de intentos
    // en el servidor; esta pantalla solo lo captura, no lo comprueba por sí misma.
    private const int LongitudCodigo = 6;

    private static readonly Brush BordeActivo = new SolidColorBrush(Color.FromArgb("#EC4899"));
    private static readonly Brush BordeNormal = new SolidColorBrush(Colors.Transparent);

    private static ServicioAutenticacion Auth => Servicios.Autenticacion;

    public VerificarCodigo()
    {
        InitializeComponent();

        CampoCodigo.TextChanged += (s, e) => AlCambiarCodigo(e.NewTextValue);
        BotonVerificar.Clicked += async (s, e) => await Verificar();
        BotonReenviar.Clicked += async (s, e) => await Reenviar();

        // Flecha: vuelve al paso anterior del flujo. Enlace: siempre al login.
        BotonAtras.Clicked += async (s, e) => await Shell.Current.GoToAsync(
            Auth.Proposito == PropositoCodigo.RecuperarClave ? "//OlvidoClave" : "//Registro");
        BotonVolver.Clicked += async (s, e) => await Shell.Current.GoToAsync("//IniciarSesion");
    }

    // Shell reutiliza la página: se reinicia cada vez que se entra.
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        TextoSubtitulo.Text = $"Te enviamos un código de {LongitudCodigo} dígitos a {Auth.CorreoPendiente}. Escríbelo para continuar.";
        CampoCodigo.Text = string.Empty;
        PintarCajas();
        CampoCodigo.Focus();   // abre el teclado numérico
    }

    // Deja solo dígitos (por si pegan "123 456") y repinta las casillas.
    private void AlCambiarCodigo(string? texto)
    {
        string soloDigitos = new((texto ?? "").Where(char.IsDigit).ToArray());

        if (soloDigitos != texto)
        {
            CampoCodigo.Text = soloDigitos;   // vuelve a disparar este método ya limpio
            return;
        }

        PintarCajas();
    }

    // Cada casilla muestra su dígito; la que toca escribir se resalta en rosa.
    private void PintarCajas()
    {
        string codigo = CampoCodigo.Text ?? "";

        for (int i = 0; i < Cajas.Children.Count; i++)
        {
            if (Cajas.Children[i] is not Border caja || caja.Content is not Label digito)
                continue;

            digito.Text = i < codigo.Length ? codigo[i].ToString() : "";
            caja.Stroke = i == codigo.Length ? BordeActivo : BordeNormal;
        }
    }

    private async Task Verificar()
    {
        string codigo = CampoCodigo.Text ?? "";

        if (codigo.Length != LongitudCodigo)
        {
            await DisplayAlertAsync("Código incompleto", $"Escribe los {LongitudCodigo} dígitos.", "OK");
            return;
        }

        BotonVerificar.IsEnabled = false;
        try
        {
            // BACKEND: si responde error (código malo, vencido o demasiados intentos), el mensaje que
            // viene en "resultado.Mensaje" es lo que se muestra al usuario tal cual.
            var resultado = await Auth.VerificarCodigoAsync(codigo);

            if (!resultado.Exito)
            {
                await DisplayAlertAsync("Código incorrecto", resultado.Mensaje, "OK");
                return;
            }

            if (Auth.Proposito == PropositoCodigo.VerificarCorreo)
            {
                await DisplayAlertAsync("¡Listo!", "Tu correo fue verificado. Ya puedes iniciar sesión.", "OK");
                await Shell.Current.GoToAsync("//IniciarSesion");
            }
            else
            {
                await Shell.Current.GoToAsync("//NuevaClave");
            }
        }
        finally
        {
            BotonVerificar.IsEnabled = true;
        }
    }

    private async Task Reenviar()
    {
        BotonReenviar.IsEnabled = false;
        try
        {
            // BACKEND: debe limitar los reenvíos (espera mínima y tope por hora) y devolver un mensaje
            // claro si se pide demasiado pronto (p. ej. 429 con { mensaje }).
            var resultado = await Auth.ReenviarCodigoAsync();

            await DisplayAlertAsync(
                resultado.Exito ? "Código enviado" : "No se pudo reenviar",
                resultado.Exito ? "Revisa tu correo, te enviamos un código nuevo." : resultado.Mensaje,
                "OK");
        }
        finally
        {
            BotonReenviar.IsEnabled = true;
        }
    }
}
