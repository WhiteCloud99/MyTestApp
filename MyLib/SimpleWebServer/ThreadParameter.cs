using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyLib.SimpleWebServer
{
  public class ThreadParameter
  {
    public HttpListenerContext Context { get; set; }
    public Thread Thread { get; set; }
  }
}
