namespace LazyMagic.OIDC.Base
{
    /// <summary>
    /// Base ViewModel class with OIDC authentication support
    /// UI-agnostic, can be used in any MVVM framework
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        protected readonly IOIDCService AuthenticationService;
        protected readonly ILogger Logger;

        private bool _isBusy;
        private string? _errorMessage;
        private OIDCAuthenticationState? _authenticationState;

        public ViewModelBase(IOIDCService authenticationService, ILogger logger)
        {
            AuthenticationService = authenticationService;
            Logger = logger;
            
            // Subscribe to authentication changes
            AuthenticationService.AuthenticationStateChanged += OnAuthenticationStateChanged;
            
            // Initialize authentication state
            _ = RefreshAuthenticationStateAsync();
        }

        /// <summary>
        /// Current authentication state
        /// </summary>
        public OIDCAuthenticationState? AuthenticationState
        {
            get => _authenticationState;
            private set => SetProperty(ref _authenticationState, value);
        }

        /// <summary>
        /// Indicates if the ViewModel is performing an operation
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Error message for display
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Convenience property to check if user is authenticated
        /// </summary>
        public bool IsAuthenticated => AuthenticationState?.IsAuthenticated ?? false;

        /// <summary>
        /// Current user name
        /// </summary>
        public string? UserName => AuthenticationState?.UserName;

        /// <summary>
        /// Current user email
        /// </summary>
        public string? UserEmail => AuthenticationState?.Email;

        /// <summary>
        /// Refreshes the authentication state
        /// </summary>
        protected async Task RefreshAuthenticationStateAsync()
        {
            try
            {
                AuthenticationState = await AuthenticationService.GetAuthenticationStateAsync();
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(UserName));
                OnPropertyChanged(nameof(UserEmail));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error refreshing authentication state");
                ErrorMessage = "Failed to get authentication state";
            }
        }

        /// <summary>
        /// Gets the current access token
        /// </summary>
        protected async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                return await AuthenticationService.GetAccessTokenAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting access token");
                ErrorMessage = "Failed to get access token";
                return null;
            }
        }

        /// <summary>
        /// Executes an action with authentication check
        /// </summary>
        protected async Task ExecuteWithAuthAsync(Func<Task> action, string? errorMessage = null)
        {
            if (!IsAuthenticated)
            {
                ErrorMessage = "You must be logged in to perform this action";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, errorMessage ?? "Error executing action");
                ErrorMessage = errorMessage ?? "An error occurred";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Executes a function with authentication check and returns result
        /// </summary>
        protected async Task<T?> ExecuteWithAuthAsync<T>(Func<Task<T>> func, string? errorMessage = null)
        {
            if (!IsAuthenticated)
            {
                ErrorMessage = "You must be logged in to perform this action";
                return default;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                return await func();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, errorMessage ?? "Error executing function");
                ErrorMessage = errorMessage ?? "An error occurred";
                return default;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnAuthenticationStateChanged(object? sender, OIDCAuthenticationStateChangedEventArgs e)
        {
            AuthenticationState = e.NewState;
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(UserName));
            OnPropertyChanged(nameof(UserEmail));
            OnAuthenticationStateChangedOverride();
        }

        /// <summary>
        /// Override this to handle authentication state changes in derived classes
        /// </summary>
        protected virtual void OnAuthenticationStateChangedOverride()
        {
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        /// <summary>
        /// Cleanup - unsubscribe from events
        /// </summary>
        public virtual void Dispose()
        {
            AuthenticationService.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }
    }
}