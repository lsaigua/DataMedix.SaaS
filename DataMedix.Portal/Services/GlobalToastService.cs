namespace DataMedix.Portal.Services
{
    public class GlobalToastService
    {
        public event Action? OnChange;

        public bool Visible { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string Type { get; private set; } = "success"; // success, error, warning, info

        public void ShowSuccess(string message)
        {
            Show(message, "success");
        }

        public void ShowError(string message)
        {
            Show(message, "error");
        }

        public void ShowWarning(string message)
        {
            Show(message, "warning");
        }

        public void ShowInfo(string message)
        {
            Show(message, "info");
        }

        public void Hide()
        {
            Visible = false;
            OnChange?.Invoke();
        }

        private void Show(string message, string type)
        {
            Message = message;
            Type = type;
            Visible = true;
            OnChange?.Invoke();

            _ = AutoHideAsync();
        }

        private async Task AutoHideAsync()
        {
            await Task.Delay(3500);
            Visible = false;
            OnChange?.Invoke();
        }
    }
}