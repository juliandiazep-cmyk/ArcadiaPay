using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using UraniumUI;

namespace Mobile.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiCommunityToolkit()
                .UseUraniumUI()
                .UseUraniumUIMaterial()
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddMaterialSymbolsFonts();
                });

#if DEBUG

    		builder.Logging.AddDebug();
#endif

#if ANDROID
            // ── CAMPOS SIN SUGERENCIAS (solo Android) ─────────────────────────────────
            // FUNCIÓN: marca TODOS los campos de texto (Entry, incluidos los de UraniumUI) con la
            // bandera "TextFlagNoSuggestions". Así Android no activa la autocorrección ni la barra de
            // sugerencias de palabras sobre el teclado. Se hace aquí, una sola vez, en lugar de
            // repetirlo en cada campo de cada pantalla.
            // DETALLES:
            //   • Se SUMA la bandera al tipo de entrada existente (| = "y además"), sin quitar lo que
            //     MAUI ya puso (correo, contraseña oculta, etc.).
            //   • Los iconos de la barra de Gboard (portapapeles, micrófono...) pertenecen al teclado
            //     del usuario; la app no puede quitarlos.
            //   • Beneficio de seguridad: la contraseña y el código no quedan en el diccionario
            //     de sugerencias del teclado.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SinSugerencias", (handler, view) =>
            {
                var tipo = handler.PlatformView.InputType;
                if ((tipo & Android.Text.InputTypes.TextFlagNoSuggestions) == 0)
                {
                    handler.PlatformView.InputType = tipo | Android.Text.InputTypes.TextFlagNoSuggestions;
                }
            });
#endif

            return builder.Build();
        }
    }
}
