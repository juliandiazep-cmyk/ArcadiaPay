using Mobile.Client.Services;

namespace Mobile.App.Views;

// PASO 3 de recuperar contraseña: el código ya fue validado; aquí se crea la clave nueva.
// El servicio la envía junto con el correo y el código, y el backend vuelve a comprobar el código.
public partial class NuevaClave : ContentPage
{
    public NuevaClave()
    {
        InitializeComponent();

        BotonGuardar.Clicked += async (s, e) => await Guardar();

        BotonAtras.Clicked += async (s, e) => await Shell.Current.GoToAsync("//VerificarCodigo");
        BotonVolver.Clicked += async (s, e) => await Shell.Current.GoToAsync("//IniciarSesion");
    }

    // Shell reutiliza la página: se limpian los campos cada vez que se entra.
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        CampoClave.Text = CampoConfirmar.Text = string.Empty;
    }

    private async Task Guardar()
    {
        string clave = CampoClave.Text ?? "";
        string confirmar = CampoConfirmar.Text ?? "";

        // Mismas reglas que el registro: entre 6 y 10 caracteres.
        // BACKEND: esta política es provisional. El backend debe imponer la suya (idealmente más exigente)
        // y esta pantalla se ajustará a ella. Además debe volver a validar el código/token aquí.
        if (clave.Length is < 6 or > 10)
        {
            await DisplayAlertAsync("Revisa tus datos", "La contraseña debe tener entre 6 y 10 caracteres.", "OK");
            return;
        }

        if (clave != confirmar)
        {
            await DisplayAlertAsync("Revisa tus datos", "Las contraseñas no coinciden.", "OK");
            return;
        }

        BotonGuardar.IsEnabled = false;
        try
        {
            // BACKEND: este envío lleva correo + código ya validado + clave nueva. Más sólido: que lleve un
            // token de un solo uso (ver nota 2 en ServicioAutenticacion). Al cambiar la clave, el backend
            // debería invalidar las sesiones abiertas y el código/token usado.
            var resultado = await Servicios.Autenticacion.RestablecerClaveAsync(clave);

            if (!resultado.Exito)
            {
                await DisplayAlertAsync("No se pudo guardar", resultado.Mensaje, "OK");
                return;
            }

            await DisplayAlertAsync("¡Listo!", "Tu contraseña fue actualizada. Inicia sesión con la nueva.", "OK");
            await Shell.Current.GoToAsync("//IniciarSesion");
        }
        finally
        {
            BotonGuardar.IsEnabled = true;
        }
    }
}
