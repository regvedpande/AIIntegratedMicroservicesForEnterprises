namespace AiEnterprise.Core.Exceptions;

public class ComplianceException : Exception
{
    public string ErrorCode { get; }
    public ComplianceException(string errorCode, string message) : base(message)
        => ErrorCode = errorCode;
}

public class DocumentAnalysisException : Exception
{
    public Guid DocumentId { get; }
    public DocumentAnalysisException(Guid documentId, string message) : base(message)
        => DocumentId = documentId;
}

public class EnterpriseNotFoundException : Exception
{
    public Guid EnterpriseId { get; }
    public EnterpriseNotFoundException(Guid enterpriseId)
        : base($"Enterprise {enterpriseId} not found.")
        => EnterpriseId = enterpriseId;
}

public class ServiceUnavailableException : Exception
{
    public string ServiceName { get; }
    public ServiceUnavailableException(string serviceName)
        : base($"Service '{serviceName}' is currently unavailable.")
        => ServiceName = serviceName;
}

public class UnauthorizedEnterpriseAccessException : Exception
{
    public UnauthorizedEnterpriseAccessException(string message) : base(message) { }
}
