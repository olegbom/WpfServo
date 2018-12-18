using System.Windows;

namespace WpfServo
{
    public class MyLine
    {
        public Point From;
        public Point To;

        public MyLine Reverce()
        {
            return new MyLine() {From = To, To = From};
        }
    }
}