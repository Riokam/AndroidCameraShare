namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Заряд телефона для страницы зрителя. Без PIN в логах.
    /// </summary>
    public interface IBatteryStatus
    {
        /// <summary>
        /// Процент 0–100. Null, если датчика нет (тесты, эмулятор без батареи).
        /// </summary>
        int? TryGetPercent();
    }
}
