namespace Mobile.App
{
    // ─────────────────────────────────────────────────────────────────────────────
    // TecladoEstado  (ARCHIVO NUEVO)
    //
    // FUNCIÓN: es un "tablero de avisos" compartido. Le dice a las pantallas si el teclado
    // del teléfono está abierto o cerrado, sin que las pantallas tengan que saber nada de Android.
    //
    // CÓMO FUNCIONA:
    //   • Quien ESCRIBE el aviso: MainActivity (Platforms/Android), que es la única que puede
    //     medir el teclado. Llama a Notificar(true) al abrirse y Notificar(false) al cerrarse.
    //   • Quien LEE el aviso: cualquier pantalla. Se suscribe al evento "Cambio" o consulta "Visible".
    //     Hoy la usa IniciarSesion para ocultar el logo grande mientras se escribe.
    //
    // ¿POR QUÉ ESTÁ EN Mobile.App Y NO EN Platforms/Android? Porque no tiene código de Android:
    // es C# común. Así las pantallas (que son multiplataforma) pueden usarlo sin errores en iOS.
    // (En iOS nadie llama a Notificar, así que Visible queda siempre en false: no pasa nada.)
    //
    // RELACIÓN CON EL BACKEND: ninguna. Es solo interfaz.
    // ─────────────────────────────────────────────────────────────────────────────
    public static class TecladoEstado
    {
        // true mientras el teclado está abierto.
        public static bool Visible { get; private set; }

        // Se dispara SOLO cuando el estado cambia (abierto ↔ cerrado). El parámetro es el nuevo estado.
        public static event Action<bool>? Cambio;

        public static void Notificar(bool visible)
        {
            // Si el estado ya era ese, no se avisa de nuevo (evita trabajo repetido).
            if (Visible == visible) return;

            Visible = visible;
            Cambio?.Invoke(visible);
        }
    }
}
