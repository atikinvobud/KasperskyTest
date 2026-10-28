using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Share;

public static class ErrorService
{
    public static string GetMessage(ErrorCode? code) => code switch
    {
        ErrorCode.VulnerabilityNotFound => "Уязвимость не найдена",
        ErrorCode.InvalidVulnerabilityName => "Название уязвимости не может быть пустым",
        ErrorCode.InvalidCriticality => "Указан недопустимый уровень критичности",
        ErrorCode.ScanTaskNotFound => "Задача сканирования не найдена",
        ErrorCode.InvalidRepositoryUrl => "Указан некорректный URL репозитория",
        ErrorCode.ScanTaskNotCancellable => "Задачу сканирования нельзя отменить",
        ErrorCode.InternalServerError => "Внутренняя ошибка сервера.",
        _ => "Неизвестная ошибка"
    };

    
public static HttpStatusCode GetStatusCode(ErrorCode? code) => code switch
{
    ErrorCode.VulnerabilityNotFound => HttpStatusCode.NotFound,
    ErrorCode.ScanTaskNotFound => HttpStatusCode.NotFound,
    ErrorCode.InvalidVulnerabilityName => HttpStatusCode.BadRequest,
    ErrorCode.InvalidCriticality => HttpStatusCode.BadRequest,
    ErrorCode.InvalidRepositoryUrl => HttpStatusCode.BadRequest,
    ErrorCode.ScanTaskNotCancellable => HttpStatusCode.Conflict,
    ErrorCode.InternalServerError => HttpStatusCode.BadRequest,
    _ => HttpStatusCode.BadRequest
};

    public static IActionResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result);

        var statusCode = GetStatusCode(result.Code);
        var objectResult = new ObjectResult(result)
        {
            StatusCode = (int)statusCode
        };
        return objectResult;
    }
}