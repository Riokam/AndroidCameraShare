namespace AndroidCameraShare.Core
{
    /// <summary>
    /// Число активных просмотров для уведомления и /health.
    /// В v1 максимум 1 сессия.
    /// </summary>
    public sealed class ViewerCounter
    {
        private int _count;
        public int Count => _count;
        public bool HasViewer => _count > 0;
        public event Action? Changed;

        /// <summary>
        /// Успешное подключение единственного зрителя.
        /// </summary>
        public void RegisterSession()
        {
            Interlocked.Exchange(ref _count, 1);
            Changed?.Invoke();
        }

        /// <summary>
        /// Зритель ушел или ошибка - сброс счетчика.
        /// </summary>
        public void Reset()
        {
            int previous = Interlocked.Exchange(ref _count, 0);
            if (previous != 0)
            {
                Changed?.Invoke();
            }
        }

    }
}
