namespace Mobile.Client.Models;

// Resultado simple de cada llamada: lo que las pantallas necesitan para decidir qué mostrar.
public record Resultado(bool Exito, string Mensaje, bool PendienteVerificar = false);
