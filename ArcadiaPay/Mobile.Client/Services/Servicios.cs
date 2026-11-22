namespace Mobile.Client.Services;

// Punto único de acceso para las pantallas.
public static class Servicios
{
    public static ServicioAutenticacion Autenticacion { get; } = new();
}
