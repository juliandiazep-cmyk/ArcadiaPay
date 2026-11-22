using System.Net.Mail;
using Mobile.Client.Services;

namespace Mobile.App.Views;

// PASO 1 de recuperar contraseña: pide el correo y el backend envía un código de 6 dígitos.
// Siguiente pantalla: VerificarCodigo → NuevaClave.
public partial class OlvidoClave : ContentPage
{
    public OlvidoClave()
    {
        InitializeComponent();

        BotonEnviar.Clicked += async (s, e) => await EnviarCodigo();

        // Flecha y enlace regresan al login ("//IniciarSesion" es la ruta de AppShell.xaml).
        BotonAtras.Clicked += async (s, e) => await Shell.Current.GoToAsync("//IniciarSesion");
        BotonVolver.Clicked += async (s, e) => await Shell.Current.GoToAsync("//IniciarSesion");
    }

    // Shell reutiliza la página: se limpia el campo cada vez que se entra.
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        CampoCorreo.Text = string.Empty;
    }

    private async Task EnviarCodigo()
    {
        string correo = CampoCorreo.Text?.Trim() ?? "";

        if (!MailAddress.TryCreate(correo, out _) || !correo.Contains('.'))
        {
            await DisplayAlertAsync("Correo no válido", "Escribe un correo válido.", "OK");
            return;
        }

        BotonEnviar.IsEnabled = false;
        try
        {
            // BACKEND: debe responder igual exista o no el correo (así no se revela quién tiene cuenta)
            // y limitar cuántas veces se puede pedir un código por correo/IP.
            var resultado = await Servicios.Autenticacion.SolicitarRecuperacionAsync(correo);

            if (!resultado.Exito)
            {
                await DisplayAlertAsync("No se pudo enviar", resultado.Mensaje, "OK");
                return;
            }

            await Shell.Current.GoToAsync("//VerificarCodigo");
        }
        finally
        {
            BotonEnviar.IsEnabled = true;
        }
    }
}
