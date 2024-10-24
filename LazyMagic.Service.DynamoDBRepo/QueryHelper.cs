using System;
using Amazon.DynamoDBv2.DocumentModel;

namespace LazyMagic.Service.DynamoDBRepo
{
    public class QueryHelper
    {
        public static QueryOperator GetOperator(string op)
        {

            switch (op)
            {
                case "BeginsWith": return QueryOperator.BeginsWith;
                case "Between": return QueryOperator.Between;
                case "Equal": return QueryOperator.Equal;
                case "GreaterThan": return QueryOperator.GreaterThan;
                case "GreaterThanOrEqual": return QueryOperator.GreaterThanOrEqual;
                default:
                    throw new System.ArgumentException("Bad DynamoDB Operator");
            }
        }
    }
}
