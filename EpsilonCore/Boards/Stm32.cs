namespace EpsilonCore.Boards;

public static class Stm32
{
    /// <summary>
    /// Returns a list of pin names starting from PA0 up to the last GPIO set letter and last pin number.
    /// </summary>
    /// <param name="lastGpioSetLetter"></param>
    /// <param name="lastPinNumber"></param>
    /// <returns></returns>
    public static IEnumerable<string> GeneratePinNames(char lastGpioSetLetter, uint lastPinNumber)
    {
        List<string> pinNames = [];
        char lastGpioSetLetterUppercase = char.ToUpper(lastGpioSetLetter);
        for (char gpioLetter = 'A'; gpioLetter <= lastGpioSetLetterUppercase; gpioLetter++)
        {
            for (uint num = 0; num <= lastPinNumber; num++)
            {
                pinNames.Add($"P{gpioLetter}{num}");
            }
        }
        return pinNames;
    }
}
