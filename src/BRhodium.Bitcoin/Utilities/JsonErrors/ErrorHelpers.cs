using System.Collections.Generic;
using System.Net;

namespace BRhodium.Bitcoin.Utilities.JsonErrors
{
    public static class ErrorHelpers
    {
        public static ErrorResult BuildErrorResponse(HttpStatusCode statusCode, string message, string description)
        {
            ErrorModel errorResponse = new ErrorModel
            {
                Status = (int)statusCode,
                ErrorCode = message,
                Description = description
            };
           

            return new ErrorResult((int)statusCode, errorResponse);
        }
    }
}
