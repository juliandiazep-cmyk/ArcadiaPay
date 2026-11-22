namespace Mobile.Client.Models;

// Forma del error que devuelve la API.
public record ErrorApi(string? Mensaje, bool PendienteVerificar);
