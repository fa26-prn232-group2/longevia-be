namespace Progress.Domain.Constants;

public static class BmiCategory
{
    public const string Underweight = "Underweight";
    public const string NormalWeight = "NormalWeight";
    public const string Overweight = "Overweight";
    public const string Obese = "Obese";

    public static string FromBmi(decimal bmi)
    {
        if (bmi < 18.5m)
        {
            return Underweight;
        }
        if (bmi < 25.0m)
        {
            return NormalWeight;
        }
        if (bmi < 30.0m)
        {
            return Overweight;
        }
        return Obese;
    }
}
