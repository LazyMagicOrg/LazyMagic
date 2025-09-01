

namespace LazyMagic.OIDC.Base
{
    /// <summary>
    /// Example ViewModel that uses authentication
    /// Completely UI-agnostic - no Blazor dependencies
    /// </summary>
    public class UserProfileViewModel : ViewModelBase
    {
        private UserProfile? _userProfile;
        private bool _isEditing;
        private string? _editName;
        private string? _editBio;

        public UserProfileViewModel(
            IOIDCService authenticationService,
            ILogger<UserProfileViewModel> logger) 
            : base(authenticationService, logger)
        {
            // Initialize commands
            LoadProfileCommand = new RelayCommand(async () => await LoadProfileAsync(), () => IsAuthenticated);
            SaveProfileCommand = new RelayCommand(async () => await SaveProfileAsync(), () => IsEditing && IsAuthenticated);
            StartEditCommand = new RelayCommand(() => Task.Run(StartEdit), () => !IsEditing && UserProfile != null);
            CancelEditCommand = new RelayCommand(() => Task.Run(CancelEdit), () => IsEditing);
            
            // Load profile if authenticated
            if (IsAuthenticated)
            {
                _ = LoadProfileAsync();
            }
        }

        #region Properties

        public UserProfile? UserProfile
        {
            get => _userProfile;
            set => SetProperty(ref _userProfile, value);
        }

        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(IsViewMode));
                    (SaveProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StartEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CancelEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsViewMode => !IsEditing;

        public string? EditName
        {
            get => _editName;
            set => SetProperty(ref _editName, value);
        }

        public string? EditBio
        {
            get => _editBio;
            set => SetProperty(ref _editBio, value);
        }

        #endregion

        #region Commands

        public ICommand LoadProfileCommand { get; }
        public ICommand SaveProfileCommand { get; }
        public ICommand StartEditCommand { get; }
        public ICommand CancelEditCommand { get; }

        #endregion

        #region Methods

        private async Task LoadProfileAsync()
        {
            await ExecuteWithAuthAsync(async () =>
            {
                // Get user claims for initial profile data
                var claims = await AuthenticationService.GetUserClaimsAsync();
                var email = claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var name = claims.FirstOrDefault(c => c.Type == "name")?.Value;
                
                // In a real app, you'd load from API
                // For demo, create a profile from claims
                UserProfile = new UserProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Email = email ?? UserEmail ?? "",
                    Name = name ?? UserName ?? "Unknown User",
                    Bio = "This is a sample bio",
                    LastLogin = DateTime.UtcNow
                };
            }, "Failed to load user profile");
        }

        private async Task SaveProfileAsync()
        {
            await ExecuteWithAuthAsync(async () =>
            {
                if (UserProfile == null) return;
                
                // Update profile with edited values
                UserProfile.Name = EditName ?? UserProfile.Name;
                UserProfile.Bio = EditBio ?? UserProfile.Bio;
                
                // In a real app, you'd save to API
                // For demo, just simulate a delay
                await Task.Delay(500);
                
                IsEditing = false;
                OnPropertyChanged(nameof(UserProfile));
            }, "Failed to save profile");
        }

        private void StartEdit()
        {
            if (UserProfile == null) return;
            
            EditName = UserProfile.Name;
            EditBio = UserProfile.Bio;
            IsEditing = true;
        }

        private void CancelEdit()
        {
            EditName = null;
            EditBio = null;
            IsEditing = false;
        }

        protected override void OnAuthenticationStateChangedOverride()
        {
            // Reload profile when authentication state changes
            if (IsAuthenticated)
            {
                _ = LoadProfileAsync();
            }
            else
            {
                UserProfile = null;
                IsEditing = false;
            }
            
            // Update command availability
            (LoadProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion
    }
}