using api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace api.Controllers;

public class InstallationAccessExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is InstallationAccessException)
        {
            context.Result = new ForbidResult();
            context.ExceptionHandled = true;
        }
    }
}
