using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MyLib.SimpleWebServer
{
  public class ActionRequestedEventArgs : EventArgs
  {
    #region Field
    private WebServer server = null;
    private HttpListenerContext context = null;
    #endregion

    #region Properties
    public WebServer Server
    {
      get { return this.server;  }
    }

    public HttpListenerContext Context
    {
      get { return this.context; }
    }
    #endregion

    #region 생성자
    public ActionRequestedEventArgs(WebServer server, HttpListenerContext context)
    {
      this.server = server;
      this.context = context;
    }
    #endregion
  }
}
