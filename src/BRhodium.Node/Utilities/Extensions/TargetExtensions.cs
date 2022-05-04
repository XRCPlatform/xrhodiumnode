using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace BRhodium.Node.Utilities.Extensions
{
    public static class TargetExtensions
    {
        public static double DifficultySafe(this Target target)
        {
            double difficulty = 0;

            try
            {
                difficulty = target.Difficulty;
            }
            catch (ArithmeticException)
            {
                //Division by zero error
            }
            catch (Exception)
            {
                //Another exception
            }

            return difficulty;
        }
    }
}
