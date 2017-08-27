using mpPInterface;

namespace mpPlinesEdit
{
    public class Interface : IPluginInterface
    {
        public string Name => "mpPlinesEdit";
        public string AvailCad => "2010";
        public string LName => "Полилинии";
        public string Description => "Сборник различных функций для работы с полилиниями";
        public string Author => "Пекшев Александр aka Modis";
        public string Price => "0";
    }

    public static class VersionData
    {
        public static string FuncVersion = "2010";
    }
}