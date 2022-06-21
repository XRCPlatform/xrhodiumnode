using System;
using System.Collections.Generic;
using System.Text;

namespace BRhodium.Node.Utilities.JsonContract
{
    public static class ResultHelper
    {
        public static ResultModel BuildResultResponse(object obj)
        {
            return BuildResultResponse(obj, string.Empty, 0);
        }

        public static ResultModel BuildResultResponse(object obj, string error, int id)
        {
            ResultModel resultModel = new ResultModel
            {
                Result = obj,
                Error = new List<ErrorModel>(),
                Id = id
            };

            if (string.IsNullOrEmpty(error))
            {
                resultModel.Error = null;
            }

            return resultModel;
        }

        public static ResultModel BuildResultResponse(object obj, ErrorModel error, int id)
        {
            ResultModel resultModel = new ResultModel
            {
                Result = obj,
                Error = error,
                Id = id
            };

            return resultModel;
        }
    }
}
