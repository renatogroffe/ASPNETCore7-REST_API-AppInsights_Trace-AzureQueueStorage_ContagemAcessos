using Microsoft.AspNetCore.Mvc;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using System.Text.Json;
using Azure.Storage.Queues;
using APIContagem.Models;

namespace APIContagem.Controllers;

[ApiController]
[Route("[controller]")]
public class ContadorController : ControllerBase
{
    private static readonly Contador _CONTADOR = new Contador();
    private readonly ILogger<ContadorController> _logger;
    private readonly IConfiguration _configuration;
    private readonly TelemetryConfiguration _telemetryConfig;

    public ContadorController(ILogger<ContadorController> logger,
        IConfiguration configuration,
        TelemetryConfiguration telemetryConfig)
    {
        _logger = logger;
        _configuration = configuration;
        _telemetryConfig = telemetryConfig;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ResultadoContador), StatusCodes.Status202Accepted)]
    public ActionResult<ResultadoContador> Get()
    {
        int valorAtualContador;

        lock (_CONTADOR)
        {
            _CONTADOR.Incrementar();
            valorAtualContador = _CONTADOR.ValorAtual;
        }

        var resultado = new ResultadoContador()
        {
            ValorAtual = valorAtualContador,
            Producer = _CONTADOR.Local,
            Kernel = _CONTADOR.Kernel,
            Framework = _CONTADOR.Framework,
            Mensagem = _configuration["MensagemVariavel"]
        };
        var messageContent = JsonSerializer.Serialize(resultado);
        var telemetryClient = new TelemetryClient(_telemetryConfig);

        var queueClient = new QueueClient(_configuration.GetConnectionString("AzureQueueStorage"),
            _configuration["AzureQueueStorage:Queue"]);
        queueClient.SendMessage(messageContent);
        telemetryClient.TrackTrace(
            $"Mensagem enviada com sucesso | {messageContent}");

        _logger.LogInformation($"Contador - Valor atual: {valorAtualContador}");
        telemetryClient.TrackTrace(
            $"Valor gerado = {resultado.ValorAtual}");

        return Accepted(resultado);
    }
}