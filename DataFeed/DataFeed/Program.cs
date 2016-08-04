using System;
using System.Collections.Generic;
using System.Text;

namespace DataFeed
{
    class Program
    {
        static void Main(string[] args)
        {
            WinSCPClient client = new WinSCPClient();
            client.SFTPUploadFiles();
        }
    }
}
