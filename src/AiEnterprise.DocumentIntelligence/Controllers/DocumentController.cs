using AiEnterprise.Core.DTOs;
using AiEnterprise.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AiEnterprise.DocumentIntelligence.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly IDocumentAnalysisService _documentService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentAnalysisService documentService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// Upload and analyze a document using Claude AI.
    /// Supports contracts, DPAs, policies, invoices, NDAs.
    /// Returns risk analysis with compliance concerns and recommendations.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<DocumentAnalysisSummary>> AnalyzeDocument(
        [FromBody] AnalyzeDocumentRequest request,
        CancellationToken ct)
    {
        if (request.EnterpriseId == Guid.Empty)
            return BadRequest(new { error = "EnterpriseId is required." });

        if (string.IsNullOrWhiteSpace(request.Base64Content))
            return BadRequest(new { error = "Document content is required." });

        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new { error = "FileName is required." });

        try
        {
            var result = await _documentService.AnalyzeDocumentAsync(request, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "Invalid Base64 content." });
        }
    }

    /// <summary>
    /// Get the full AI analysis result for a previously analyzed document.
    /// </summary>
    [HttpGet("{documentId}/analysis")]
    public async Task<ActionResult<DocumentAnalysisResult>> GetAnalysis(Guid documentId, CancellationToken ct)
    {
        var result = await _documentService.GetAnalysisResultAsync(documentId, ct);
        if (result is null) return NotFound(new { error = "Document analysis not found." });
        return Ok(result);
    }

    /// <summary>
    /// Get a paginated list of documents for an enterprise.
    /// </summary>
    [HttpGet("{enterpriseId}/list")]
    public async Task<ActionResult<PagedResult<DocumentAnalysisSummary>>> GetDocuments(
        Guid enterpriseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { error = "Page must be >= 1 and pageSize between 1-100." });

        var result = await _documentService.GetDocumentSummariesAsync(enterpriseId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { Status = "Healthy", Service = "DocumentIntelligence", Timestamp = DateTime.UtcNow });
}
