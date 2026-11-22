using Mobile.Client.Services;

namespace Mobile.App.Views;

public partial class MenuPrincipal : ContentPage
{
    // ───────────────────────── LÓGICA DE NAVEGACIÓN ─────────────────────────
    //
    // Todo vive en esta sola página. Las 5 secciones están dentro del Grid "Secciones"
    // (en el XAML), una encima de otra, y solo una es visible a la vez.
    // Al tocar un botón de la barra, cambiamos cuál sección es visible.

    // Índice de la pestaña activa (-1 = ninguna todavía).
    private int _indiceActual = -1;

    // Colores del diseño (los mismos del inicio de sesión).
    private static readonly Color ColorInactivo = Color.FromArgb("#9AA3AE");
    private static readonly Color ColorActivo = Colors.White;

    // Degradado rosa → naranja del botón "Iniciar sesión", usado para la pestaña activa.
    private static readonly Brush FondoActivo = new LinearGradientBrush(
    [
        new GradientStop(Color.FromArgb("#EC4899"), 0.0f),
        new GradientStop(Color.FromArgb("#F97316"), 1.0f),
    ],
    new Point(0, 0), new Point(1, 0));

    private static readonly Brush FondoInactivo = new SolidColorBrush(Colors.Transparent);

    public MenuPrincipal()
    {
        InitializeComponent();

        // Conecta el toque de cada botón de la barra.
        ConectarToques();

        // Botón "Cerrar sesión" de la pestaña Cuenta.
        BotonCerrarSesion.Clicked += async (s, e) => await CerrarSesion();

        // Al abrir el menú se muestra la primera pestaña (Inicio), sin animación.
        _ = MostrarSeccion(0, animar: false);
    }

    // CERRAR SESIÓN (temporal)
    // 1) Pide confirmación. 2) Le avisa al servicio que la sesión terminó.
    // 3) Deja el menú listo en "Inicio" para el próximo ingreso (Shell reutiliza esta
    //    misma página, si no se reinicia volvería a abrir en la pestaña Cuenta).
    // 4) Regresa al login; desde ahí el usuario puede volver a iniciar sesión.
    private async Task CerrarSesion()
    {
        bool confirmar = await DisplayAlertAsync(
            "Cerrar sesión", "¿Seguro que quieres salir?", "Sí, salir", "Cancelar");
        if (!confirmar) return;

        Servicios.Autenticacion.CerrarSesion();
        await MostrarSeccion(0, animar: false);
        await Shell.Current.GoToAsync("//IniciarSesion");
    }

    // PASO 1 · A cada botón de la barra se le asigna un toque que llama a MostrarSeccion
    // con su índice (el ClassId que tiene en el XAML: 0 a 4).
    private void ConectarToques()
    {
        foreach (var hijo in BarraNav.Children)
        {
            if (hijo is Border boton && int.TryParse(boton.ClassId, out int indice))
            {
                boton.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command(async () => await MostrarSeccion(indice, animar: true))
                });
            }
        }
    }

    // PASO 2 · Muestra solo la sección elegida y oculta las demás.
    private async Task MostrarSeccion(int indice, bool animar)
    {
        var secciones = this.FindByName<Grid>("Secciones");

        // Si toca la pestaña que ya está activa, no hacemos nada.
        if (indice == _indiceActual) return;

        // Efecto suave: desvanece, cambia la sección visible y vuelve a aparecer.
        if (animar)
            await secciones.FadeToAsync(0, 90);

        for (int i = 0; i < secciones.Children.Count; i++)
        {
            if (secciones.Children[i] is VisualElement seccion)
                seccion.IsVisible = (i == indice);
        }


        if (animar)
            await secciones.FadeToAsync(1, 140);

        _indiceActual = indice;

        // PASO 3 · Actualiza la barra para resaltar la pestaña activa.
        ActualizarBarra(indice);
    }

    // PASO 3 · Pinta de degradado el botón activo y deja los demás en gris.
    private void ActualizarBarra(int indiceActivo)
    {
        foreach (var hijo in BarraNav.Children)
        {
            if (hijo is not Border boton || boton.Content is not VerticalStackLayout contenido)
                continue;

            bool esActivo = boton.ClassId == indiceActivo.ToString();

            // Fondo: degradado si está activo, transparente si no.
            boton.Background = esActivo ? FondoActivo : FondoInactivo;

            // Ícono y texto: blancos si está activo, grises si no.
            foreach (var etiqueta in contenido.Children.OfType<Label>())
                etiqueta.TextColor = esActivo ? ColorActivo : ColorInactivo;
        }
    }
}