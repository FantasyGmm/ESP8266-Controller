namespace ESP8266_Controller_WPF
{
    [PropertyChanged.AddINotifyPropertyChangedInterface]
    public class PlayImage
    {
        private bool isPlaying;
        public bool IsPlaying
        {
            get => isPlaying;
            set
            {
                CanInput = !value;
                isPlaying = value;
            }
        }

        public bool IsPuase { get; set; } = false;
        public bool CanInput { get; set; } = true;

    }
}
