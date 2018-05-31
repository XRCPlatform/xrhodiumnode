using System;
using System.Collections.Generic;
using System.Text;

namespace BRhodium.Bitcoin.Utilities.JsonContract
{
    public static class ResultHelper
    {
        public static ResultModel BuildResultResponse(object obj)
        {
            if (obj is ErrorModel)
            {
                ResultModel resultModel = new ResultModel
                {
                    Result = null,
                    Error = obj,
                    Id = 0
                };
                return resultModel;
            }
            return new ResultModel
            {
                Result = obj,
                Error = null,
                Id = 0
            };
        }
        public static ResultModel BuildResultResponse(object obj, ErrorModel err)
        {
            return new ResultModel
            {
                Result = obj,
                Error = err,
                Id = 0
            };

        }
    }
}
