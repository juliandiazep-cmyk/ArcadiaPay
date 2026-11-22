using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using ARect = Android.Graphics.Rect;
using AView = Android.Views.View;

namespace Mobile.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // ACTIVIDAD PRINCIPAL DE ANDROID: aquí vive el manejo del teclado.
    //
    // PROBLEMA: en Android moderno la app ocupa toda la pantalla y el teclado se dibuja
    // ENCIMA de la interfaz, tapando campos y botones.
    //
    // SOLUCIÓN (3 piezas):
    //   1) WindowSoftInputMode = AdjustResize  → le pide a Android que ajuste la ventana
    //      cuando sale el teclado (respaldo; en pantallas completas Android lo ignora).
    //   2) TecladoListener (abajo)             → mide el teclado y le pone un relleno
    //      inferior a toda la interfaz, para que SUBA y el teclado quede debajo.
    //   3) Desplazamiento automático           → mueve la pantalla hasta el campo que
    //      se está escribiendo, como hacen las apps nativas.
    // ─────────────────────────────────────────────────────────────────────────────
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustResize, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // Guardamos el listener para poder quitarlo cuando la actividad se destruya.
        private TecladoListener? _listener;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // "Content" es la vista raíz de Android que contiene toda la app MAUI.
            // A ella le ponemos el relleno cuando aparece el teclado.
            var contenido = FindViewById(Android.Resource.Id.Content);
            var observer = Window?.DecorView?.ViewTreeObserver;

            if (contenido != null && observer != null && Window?.DecorView != null)
            {
                _listener = new TecladoListener(this, Window.DecorView, contenido);

                // Se avisa cada vez que Android reorganiza la pantalla (por ejemplo, al salir el teclado)...
                observer.AddOnGlobalLayoutListener(_listener);
                // ...y cada vez que el foco pasa de un campo a otro.
                observer.AddOnGlobalFocusChangeListener(_listener);
            }
        }

        protected override void OnDestroy()
        {
            // Limpieza: se quitan los listeners para no dejar memoria ocupada.
            var observer = Window?.DecorView?.ViewTreeObserver;
            if (_listener != null && observer != null)
            {
                observer.RemoveOnGlobalLayoutListener(_listener);
                observer.RemoveOnGlobalFocusChangeListener(_listener);
            }
            _listener = null;
            base.OnDestroy();
        }

        // Clase interna que hace el trabajo. Implementa dos "avisos" de Android:
        //   • IOnGlobalLayoutListener      → "la pantalla se reorganizó"
        //   • IOnGlobalFocusChangeListener → "cambió el campo con foco"
        // (Se usa "AView" y "ARect" porque MAUI también tiene clases llamadas View y Rect,
        //  y el compilador no sabría cuál elegir.)
        private sealed class TecladoListener : Java.Lang.Object,
            ViewTreeObserver.IOnGlobalLayoutListener,
            ViewTreeObserver.IOnGlobalFocusChangeListener
        {
            private readonly Activity _actividad;
            private readonly AView _decor;       // la ventana completa (incluye barras y teclado)
            private readonly AView _contenido;   // el área donde se dibuja la app
            private readonly int[] _ubicacion = new int[2];  // se reutiliza para no crear objetos cada vez
            private readonly int _margenPx;      // espacio extra sobre el campo al desplazar (24 dp en píxeles)
            private bool _tecladoVisible;        // recuerda si el teclado ya estaba abierto

            public TecladoListener(Activity actividad, AView decor, AView contenido)
            {
                _actividad = actividad;
                _decor = decor;
                _contenido = contenido;

                // Convierte 24 dp a píxeles según la densidad de la pantalla.
                _margenPx = (int)(24 * actividad.Resources!.DisplayMetrics!.Density);
            }

            // Se ejecuta cada vez que Android reorganiza la pantalla.
            public void OnGlobalLayout()
            {
                // 1) ¿Qué altura tiene el teclado ahora? (0 si está cerrado)
                int alturaTeclado = ViewCompat.GetRootWindowInsets(_decor)
                    ?.GetInsets(WindowInsetsCompat.Type.Ime())?.Bottom ?? 0;

                // 2) Calcula cuánto relleno inferior necesita la interfaz.
                int relleno = 0;
                if (alturaTeclado > 0)
                {
                    // Compara dónde termina la app con dónde empieza el teclado
                    // (ambos medidos desde la parte superior de la ventana).
                    // Si la app llega hasta abajo, el relleno es la altura del teclado.
                    // Si Android ya la achicó por su cuenta, el relleno sale 0 y no se suma doble.
                    _contenido.GetLocationInWindow(_ubicacion);
                    int inferiorContenido = _ubicacion[1] + _contenido.Height;
                    int superiorTeclado = _decor.Height - alturaTeclado;
                    relleno = Math.Max(0, inferiorContenido - superiorTeclado);
                }

                bool visible = alturaTeclado > 0;
                bool cambioRelleno = _contenido.PaddingBottom != relleno;

                // 3) Avisa a las pantallas cuando el teclado se abre o se cierra
                //    (Iniciar sesión lo usa para ocultar el logo grande).
                if (visible != _tecladoVisible)
                {
                    TecladoEstado.Notificar(visible);
                }

                // 4) Aplica el relleno: así la interfaz SUBE y el teclado queda debajo.
                //    Solo se toca si el valor cambió (evita trabajo innecesario).
                if (cambioRelleno)
                {
                    _contenido.SetPadding(_contenido.PaddingLeft, _contenido.PaddingTop, _contenido.PaddingRight, relleno);
                }

                // 5) Al abrirse el teclado (o cambiar de tamaño) lleva la pantalla al campo activo.
                if (visible && (cambioRelleno || !_tecladoVisible))
                {
                    DesplazarAlCampoActivo();
                }

                _tecladoVisible = visible;
            }

            // Se ejecuta cuando el foco pasa a otro campo (por ejemplo, con la tecla "Siguiente").
            public void OnGlobalFocusChanged(AView? anterior, AView? nuevo)
            {
                // Solo importa si el teclado ya está abierto; si no, lo hará OnGlobalLayout al abrirse.
                if (_tecladoVisible && nuevo != null)
                {
                    DesplazarAlCampoActivo();
                }
            }

            // Pide a Android que haga visible el campo con foco dentro del ScrollView.
            private void DesplazarAlCampoActivo()
            {
                // Se espera 60 ms para que termine de reorganizarse la pantalla
                // (relleno aplicado y logo oculto) antes de calcular a dónde desplazar.
                _contenido.PostDelayed(() =>
                {
                    var foco = _actividad.CurrentFocus;
                    if (foco == null || foco.Width == 0) return;

                    // Rectángulo del campo, ampliado hacia arriba para que no quede pegado al borde.
                    var rect = new ARect(0, -_margenPx, foco.Width, foco.Height + _margenPx);
                    foco.RequestRectangleOnScreen(rect, false);
                }, 60);
            }
        }
    }
}
