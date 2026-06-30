namespace VirtualPartner.Runtime.PhoneOS
{
    public interface IPhoneApp
    {
        string AppId { get; }

        void OnOpen(object args = null);

        void OnClose();

        void OnPause();

        void OnResume();

        bool OnBackPressed();
    }
}
