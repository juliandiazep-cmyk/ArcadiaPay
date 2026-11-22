using System.Net.Mail;
using Mobile.Client.Services;

namespace Mobile.App.Views;

public partial class Registro : ContentPage
{
    public Registro()
    {
        InitializeComponent();

        // Los botones se conectan por código (no en el XAML).
        BotonCrearCuenta.Clicked += async (s, e) => await CrearCuenta();

        // "Iniciar sesión" (arriba) regresa a la pantalla de login.
        BotonIrLogin.Clicked += async (s, e) => await Shell.Current.GoToAsync("//IniciarSesion");
    }

    private async Task CrearCuenta()
    {
        string nombre = CampoNombre.Text?.Trim() ?? "";
        string cedula = CampoCedula.Text?.Trim() ?? "";
        string correo = CampoCorreo.Text?.Trim() ?? "";
        string clave = CampoClave.Text ?? "";
        string confirmar = CampoConfirmar.Text ?? "";

        // 1) Validaciones: si algo falla, se avisa y se detiene el registro.
        string? error = Validar(nombre, cedula, correo, clave, confirmar);
        if (error is not null)
        {
            await DisplayAlertAsync("Revisa tus datos", error, "OK");
            return;
        }

        BotonCrearCuenta.IsEnabled = false;
        try
        {
            // 2) El backend crea la cuenta (sin verificar) y envía el código al correo.
            var resultado = await Servicios.Autenticacion.RegistrarAsync(nombre, cedula, correo, clave);

            if (!resultado.Exito)
            {
                await DisplayAlertAsync("No se pudo crear la cuenta", resultado.Mensaje, "OK");
                return;
            }

            // 3) Cuenta creada: se limpia el formulario y se pide el código del correo.
            //    "//VerificarCodigo" es la ruta definida en AppShell.xaml.
            LimpiarCampos();
            await Shell.Current.GoToAsync("//VerificarCodigo");
        }
        finally
        {
            BotonCrearCuenta.IsEnabled = true;
        }
    }

    // Devuelve el mensaje del primer problema encontrado, o null si todo está bien.
    // BACKEND: estas reglas son solo una ayuda rápida para el usuario. El backend debe repetir TODAS las
    // validaciones (cédula, correo, política de clave) y además impedir correos o cédulas duplicadas.
    private static string? Validar(string nombre, string cedula, string correo,
                                   string clave, string confirmar)
    {
        if (nombre == "" || cedula == "" || correo == "" || clave == "" || confirmar == "")
            return "Completa todos los campos.";

        if (!cedula.All(char.IsDigit) || cedula.Length < 6 || cedula.Length > 9)
            return "La cédula debe tener solo números (entre 6 y 9 dígitos).";

        if (!MailAddress.TryCreate(correo, out _) || !correo.Contains('.'))
            return "El correo no es válido.";

        // El login limita la contraseña a 10 caracteres, por eso aquí también.
        if (clave.Length < 6 || clave.Length > 10)
            return "La contraseña debe tener entre 6 y 10 caracteres.";

        if (clave != confirmar)
            return "Las contraseñas no coinciden.";

        return null;
    }

    private void LimpiarCampos()
    {
        CampoNombre.Text = CampoCedula.Text = CampoCorreo.Text =
            CampoClave.Text = CampoConfirmar.Text = string.Empty;
    }
}
