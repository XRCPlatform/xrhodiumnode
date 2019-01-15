using System.Collections.Generic;
using System.Net;

namespace BRhodium.Node.Utilities.JsonErrors
{
    public static class ErrorHelpers
    {
        public static ErrorResult BuildErrorResponse(HttpStatusCode statusCode, string message, string description)
        {
            ErrorModel errorResponse = new ErrorModel
            {
                Status = (int)statusCode,
                ErrorCode = message,
#if DEBUG
                Description = description
#endif
#if !DEBUG
                // The description can contain exception backtrace data
                // which is inappropriate for a release build.
                Description = ""
#endif
            };
           

            return new ErrorResult((int)statusCode, errorResponse);
        }
    }
}
