using Microsoft.AspNetCore.Mvc;

namespace BRhodium.Bitcoin.Utilities.JsonErrors
{
    public class ErrorResult : ObjectResult
    {
        public ErrorResult(int statusCode, ErrorModel value) : base(value)
        {
            this.StatusCode = statusCode;
        }
    }
}
