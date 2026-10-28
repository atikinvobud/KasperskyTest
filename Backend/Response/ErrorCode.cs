namespace Backend.Share;

public enum ErrorCode
{
    VulnerabilityNotFound,
    InvalidVulnerabilityName,
    InvalidCriticality,
    ScanTaskNotFound,
    InvalidRepositoryUrl,
    ScanTaskNotCancellable,
    InternalServerError
}