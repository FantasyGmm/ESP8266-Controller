namespace ESP8266_Controller
{
    public class HardInfoItem
    {
        public string contStr;
        public string labelStr;
        public string unitStr;
        public string proportionStr;
        public override string ToString()
        {
            return labelStr + " " + proportionStr + " " + unitStr;
        }
    }
}
