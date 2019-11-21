namespace mpPlinesEdit
{
    using System.Windows.Media.Imaging;

    /// <summary>
    /// Данные функции для отображения в главном окне
    /// </summary>
    public class PlinesFunction
    {
        /// <summary>
        /// Имя функции
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Локализованное имя функции
        /// </summary>
        public string LocalName { get; set; }

        /// <summary>
        /// Описание
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Обработка легких полилиний
        /// </summary>
        public bool Plw { get; set; }

        /// <summary>
        /// Обработка 3D полилиний
        /// </summary>
        public bool P3D { get; set; }

        /// <summary>
        /// Обработка 2D полилиний
        /// </summary>
        public bool P2D { get; set; }

        /// <summary>
        /// Большая иконка
        /// </summary>
        public BitmapImage ImageBig { get; set; }

        /// <summary>
        /// Маленькая иконка
        /// </summary>
        public BitmapImage ImageSmall { get; set; }

        /// <summary>
        /// Маленькая иконка для темной темы оформления
        /// </summary>
        public BitmapImage ImageDarkSmall { get; set; }
    }
}