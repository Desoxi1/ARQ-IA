using System.Text.RegularExpressions;

namespace BimAiDynamoAssistant;

/// <summary>
/// Clasificador local (0 tokens): decide si un pedido debe ir a Codex o si
/// conviene pedir una aclaración antes de gastar un turno. Solo intercepta
/// escrituras ambiguas evidentes; jamás bloquea lecturas ni preguntas.
/// </summary>
internal static class TaskRouter
{
    // "píntame el piso 1 de rojo": nivel/planta + color sin anclas concretas.
    private static readonly Regex AmbiguousPaintTarget =
        new(@"\b(piso|planta|nivel)\b.{0,40}\b(pinta|pintar|p[ií]ntame|color|rojo|verde|azul|amarillo)\b|\b(pinta|pintar|p[ií]ntame)\b.{0,40}\b(piso|planta|nivel|suelo|muro)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Anclas que vuelven concreto el pedido: material, vista/override, IDs o selección dada.
    private static readonly Regex ConcreteAnchor =
        new(@"\b(material|vista|override|plantilla|tipo\s de|family|par[aá]metro)\b|\bID\b|ids?\s*:\s*\d|selecci[oó]n\s+(actual|dada)|elementos?\s+\d+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// True cuando el pedido es una escritura ambigua que AGENTS.md obliga a
    /// aclarar antes de actuar (piso 1 = ¿nivel? ¿vista?; rojo = ¿vista o material?).
    /// </summary>
    public static bool NeedsClarification(string prompt) =>
        !string.IsNullOrWhiteSpace(prompt) &&
        AmbiguousPaintTarget.IsMatch(prompt) &&
        !ConcreteAnchor.IsMatch(prompt);

    public static string ClarifyingMessage(string prompt) =>
        "Antes de gastar un turno de Codex y por seguridad del modelo, aclaremos el alcance (AGENTS.md: sin adivinar decisiones de diseño):\n" +
        "1. ¿“Piso 1” es el Nivel 1 del modelo, una vista de planta concreta, o los elementos que tienes seleccionados? Si es un nivel, dime su nombre exacto como aparece en Revit.\n" +
        "2. ¿Rojo como override gráfico solo en la vista activa, o como cambio de material/tipo (afecta todas las vistas)?\n" +
        "3. ¿Todos los suelos del nivel o solo algunos IDs? Si son algunos, selecciónalos en Revit antes de reenviar.\n" +
        "Con esas tres respuestas preparo la rutina acotada y la verificamos en el RVT descartable.";
}
