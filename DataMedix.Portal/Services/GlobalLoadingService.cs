namespace DataMedix.Portal.Services
{
    public class GlobalLoadingService
    {
        public event Action? OnChange;

        public bool IsLoading { get; private set; }
        public string Message { get; private set; } = "Procesando...";

        public void Show(string? message = null)
        {
            IsLoading = true;
            Message = string.IsNullOrWhiteSpace(message) ? "Procesando..." : message;
            OnChange?.Invoke();
        }

        public void Hide()
        {
            IsLoading = false;
            OnChange?.Invoke();
        }
    }
}