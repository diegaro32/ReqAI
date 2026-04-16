using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace PainFinder.Infrastructure.Services;

/// <summary>
/// Limpia una conversación con el cliente eliminando todo lo que no aporte
/// información útil para la recopilación de requerimientos de un sistema de información.
/// </summary>
public class AiConversationCleanerService(
    IChatClient chatClient,
    ILogger<AiConversationCleanerService> logger)
{
    public async Task<string> CleanAsync(string conversationInput, CancellationToken cancellationToken = default)
    {
        var prompt = BuildCleaningPrompt(conversationInput);

        logger.LogInformation("Cleaning conversation ({Chars} chars)", conversationInput.Length);

        try
        {
            var response = await chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                cancellationToken: cancellationToken);

            var aiCleaned = response.Messages[^1].Text?.Trim() ?? conversationInput;

            if (string.IsNullOrWhiteSpace(aiCleaned))
                return NormalizeWhitespace(conversationInput);

            var result = NormalizeWhitespace(aiCleaned);

            logger.LogInformation("Conversation cleaned: {Original} → {Cleaned} chars",
                conversationInput.Length, result.Length);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Conversation cleaning failed, using original input");
            return NormalizeWhitespace(conversationInput);
        }
    }

    /// <summary>
    /// Post-procesamiento en código: elimina fechas, horas y colapsa espacios/saltos de línea extra.
    /// </summary>
    private static string NormalizeWhitespace(string text)
    {
        // Eliminar timestamps comunes: [10:35], 10:35:22, 10:35 AM, 10/04/2025, 2025-04-10, etc.
        text = Regex.Replace(text, @"\b\d{1,2}:\d{2}(:\d{2})?(\s?(AM|PM|am|pm))?\b", string.Empty);
        text = Regex.Replace(text, @"\b\d{1,2}[\/\-\.]\d{1,2}[\/\-\.]\d{2,4}\b", string.Empty);
        text = Regex.Replace(text, @"\b\d{4}[\/\-\.]\d{1,2}[\/\-\.]\d{1,2}\b", string.Empty);

        // Eliminar corchetes o paréntesis que queden vacíos tras quitar fechas/horas
        text = Regex.Replace(text, @"[\[\(]\s*[\]\)]", string.Empty);

        // Colapsar más de un salto de línea consecutivo en uno solo
        text = Regex.Replace(text, @"\n{2,}", "\n");

        // Eliminar espacios al inicio y fin de cada línea
        text = string.Join("\n", text.Split('\n').Select(l => l.Trim()));

        // Eliminar líneas vacías que hayan quedado
        text = Regex.Replace(text, @"\n{2,}", "\n");

        return text.Trim();
    }

    private static string BuildCleaningPrompt(string conversationInput) => $$"""
        Eres un analista de sistemas experto en recopilación de requerimientos de software.

        Tu tarea es LIMPIAR la siguiente conversación para dejar únicamente el contenido útil para desarrollar un sistema de información. Debes ELIMINAR:

        - Saludos, despedidas y presentaciones ("Hola", "Buenos días", "Hasta luego", "Un placer", etc.)
        - Charla trivial sin relación al sistema ("¿Cómo estás?", "¿Viste el partido?", comentarios del clima, etc.)
        - Confirmaciones y respuestas vacías ("Ok", "Entendido", "Claro", "Perfecto", "De acuerdo", "Sí sí", etc.)
        - Fechas, horas y marcas de tiempo dentro del texto conversacional
        - Agradecimientos que no contengan información ("Gracias", "Muchas gracias", "De nada", etc.)
        - Frases de relleno que no aporten datos del sistema ("Mmm", "Bueno...", "Ehh", "Como te decía", etc.)
        - Cualquier fragmento que no describa funcionalidades, reglas de negocio, restricciones, usuarios, flujos o contexto del sistema

        Lo que DEBES conservar exactamente como está:
        - Descripción de funcionalidades y módulos del sistema
        - Reglas de negocio y restricciones
        - Roles de usuarios y sus permisos
        - Flujos de trabajo y procesos
        - Restricciones técnicas, de tiempo o presupuesto
        - Preguntas y aclaraciones sobre el sistema
        - Contexto del negocio que explique la necesidad del sistema

        Reglas de formato:
        - Conserva el formato "nombre: mensaje" en cada línea
        - NO reformules, resumas ni parafrasees el texto que conservas: cópialo literal
        - NO añadas texto propio, títulos ni explicaciones
        - NO dejes líneas completamente vacías entre mensajes
        - Devuelve SOLO el texto limpio

        CONVERSACIÓN:
        {{conversationInput}}
        """;
}
