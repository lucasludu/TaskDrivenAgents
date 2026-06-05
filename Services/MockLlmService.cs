using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TaskDrivenAgent.Services
{
    public class MockLlmService : ILlmService
    {
        public string Model { get; set; } = "mock";
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public Task<string> GetCompletionAsync(List<ChatMessage> messages)
        {
            // Get the last message in history to determine the state
            var lastMessage = messages.LastOrDefault();
            if (lastMessage == null)
            {
                return Task.FromResult("Thought: No object defined.\nFinal Answer: Error.");
            }

            string content = lastMessage.Content;

            // SplitMoney Flow
            var userObjective = messages.FirstOrDefault(m => m.Role == "user")?.Content ?? "";
            bool isSplitMoneyTask = userObjective.Contains("SplitMoney", StringComparison.OrdinalIgnoreCase);

            if (isSplitMoneyTask)
            {
                // Step 1: Start SplitMoney API
                if (lastMessage.Role == "user" && !content.Contains("Observation:"))
                {
                    return Task.FromResult(
                        "Thought: El objetivo es iniciar el servicio SplitMoney y listar los grupos. Primero, levantaré el proceso de la API en segundo plano usando la herramienta StartSplitMoney.\n" +
                        "Action: StartSplitMoney()"
                    );
                }

                // Step 2: Query Groups from the API
                if (content.Contains("Observation: SplitMoney API started"))
                {
                    return Task.FromResult(
                        "Thought: La API de SplitMoney ha sido levantada y está corriendo en https://localhost:7042. Ahora realizaré una petición HTTP GET para obtener los grupos activos usando la herramienta CallSplitMoneyApi.\n" +
                        "Action: CallSplitMoneyApi({\"endpoint\": \"/api/v1/Groups\", \"method\": \"GET\"})"
                    );
                }

                // Step 3: Complete with groups listing or handle connection status
                if (content.Contains("Observation: Status Code:") || content.Contains("Error calling SplitMoney API:"))
                {
                    return Task.FromResult(
                        "Thought: He intentado conectar con la API de SplitMoney. Si la API aún no terminó de iniciar/compilar en la ventana secundaria, informaré al usuario para que aguarde unos segundos adicionales y reintente. La tarea de orquestación ha finalizado.\n" +
                        "Final Answer: API de SplitMoney levantada en segundo plano de manera autónoma. Al consultar la lista de grupos activos, se obtuvo la siguiente respuesta:\n" +
                        $"  [{content}]\n\n" +
                        "  (Nota: Si obtuviste un error de conexión denegada, es normal debido a que la compilación de la API de SplitMoney en .NET 10 puede demorar unos segundos en iniciar en su ventana independiente. Una vez levantada del todo en el puerto 7042, las consultas responderán con status 200 OK)."
                    );
                }
            }

            // Step 1: Initial Prompt
            if (lastMessage.Role == "user" && !content.Contains("Observation:"))
            {
                return Task.FromResult(
                    "Thought: Para resolver este objetivo, primero necesito saber qué archivos de log de errores existen en el directorio. Usaré la herramienta ListLogs.\n" +
                    "Action: ListLogs()"
                );
            }

            // Step 2: After Listing Logs
            if (content.Contains("Observation: Found files:"))
            {
                return Task.FromResult(
                    "Thought: He encontrado el archivo 'production-errors.log' en el directorio de logs. Ahora debo leer su contenido para analizar los errores de la última hora. Usaré la herramienta ReadLogFile.\n" +
                    "Action: ReadLogFile(production-errors.log)"
                );
            }

            // Step 3: After Reading Log File
            if (content.Contains("Observation: Content of production-errors.log:"))
            {
                // We create a detailed report using WriteReport tool
                var reportJson = @"{" +
                                 @"""fileName"": ""reporte_errores.md""," +
                                 @"""content"": ""# Reporte de Incidentes de Producción - Última Hora\n\n" +
                                 @"**Generado por:** Agente Autónomo de Tareas\n" +
                                 @"**Fecha del Análisis:** 2026-06-05 (Última hora analizada)\n\n" +
                                 @"## Resumen Ejecutivo\n" +
                                 @"Se analizaron los logs de producción de la última hora. Se encontraron **6 fallos** en total, distribuidos en **3 categorías** de excepciones diferentes.\n\n" +
                                 @"| Excepción | Ocurrencias | Gravedad | Impacto |\n" +
                                 @"| :--- | :---: | :---: | :--- |\n" +
                                 @"| `DatabaseConnectionException` | 2 | Crítica | Inoperabilidad de base de datos para consultas. |\n" +
                                 @"| `NullReferenceException` | 3 | Alta | Error de autenticación y carga de perfil de usuarios. |\n" +
                                 @"| `TimeoutException` | 1 | Media | Retardo/Fallo en la verificación de pagos. |\n\n" +
                                 @"---\n\n" +
                                 @"## Detalle de Excepciones y Análisis\n\n" +
                                 @"### 1. DatabaseConnectionException (Crítico)\n" +
                                 @"- **Ocurrencias:** 2\n" +
                                 @"- **Mensaje de Error:** `Could not connect to database server at 10.0.0.5 after 15s`\n" +
                                 @"- **Timestamps:** 12:45:10, 12:48:22\n" +
                                 @"- **Recomendación:** Verificar el estado de la base de datos en `10.0.0.5`. El fallo ocurrió dos veces consecutivas en un lapso de 3 minutos, lo que sugiere una desconexión o saturación temporal de sockets.\n\n" +
                                 @"### 2. NullReferenceException (Alto)\n" +
                                 @"- **Ocurrencias:** 3\n" +
                                 @"- **Mensaje de Error:** `Object reference not set to an instance of an object` en `AuthController.cs:line 45`\n" +
                                 @"- **Timestamps:** 12:15:00, 12:22:15, 12:30:40\n" +
                                 @"- **Recomendación:** Revisar el flujo de datos en el controlador de autenticación. Es probable que se esté intentando leer alguna propiedad de un objeto `User` o `TokenRequest` nulo que viene en la solicitud del cliente.\n\n" +
                                 @"### 3. TimeoutException (Medio)\n" +
                                 @"- **Ocurrencias:** 1\n" +
                                 @"- **Mensaje de Error:** `Gateway timeout connecting to external API` en Stripe payment handler\n" +
                                 @"- **Timestamp:** 12:50:00\n" +
                                 @"- **Recomendación:** Comprobar la latencia de red hacia la API externa de Stripe. Si se repite frecuentemente, implementar una política de reintentos (retry policy) con Polly.\n\n" +
                                 @"---\n" +
                                 @"*Fin del reporte generado autónomamente.*""" +
                                 @"}";

                return Task.FromResult(
                    "Thought: He analizado detenidamente los logs del archivo 'production-errors.log'. He detectado 2 DatabaseConnectionException, 3 NullReferenceException y 1 TimeoutException. " +
                    "Procederé a agrupar los fallos y escribir un reporte Markdown bien detallado con un cuadro resumen y recomendaciones en el archivo 'reporte_errores.md'. Usaré la herramienta WriteReport.\n" +
                    $"Action: WriteReport({reportJson})"
                );
            }

            // Step 4: After Writing Report
            if (content.Contains("Observation: Successfully wrote report"))
            {
                return Task.FromResult(
                    "Thought: El reporte ha sido escrito exitosamente en 'reporte_errores.md' de forma estructurada. He completado todos los requerimientos de la tarea: analizar logs, agrupar por excepción y generar el reporte.\n" +
                    "Final Answer: Tarea completada con éxito. El reporte consolidado de logs ha sido guardado en el archivo 'reporte_errores.md' en la raíz del proyecto. El reporte agrupa un total de 6 fallos distribuidos en: NullReferenceException (3), DatabaseConnectionException (2) y TimeoutException (1), junto con sugerencias de mitigación para cada caso."
                );
            }

            // Fallback
            return Task.FromResult(
                "Thought: No reconozco el estado actual. Finalizaré para evitar bucles.\n" +
                "Final Answer: Tarea suspendida por falta de contexto."
            );
        }
    }
}
