using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using MyLib.SimpleWebServer;

namespace ConsoleApp
{
  class Program
  {
    private static readonly ILog log = LogManager.GetLogger(typeof(Program));

    private static WebServer server = null;

    static void Main(string[] args)
    {
      Console.WriteLine("hi test");

      WebTest();

      server.Start();

      Console.ReadKey();

      server.Stop();
      server.Dispose();
    }

    private static void LogTest()
    {
      log.Debug("Debug");
      log.Info("Info");
      log.Warn("Warn");
      log.Error("Error");
      log.Fatal("Fatal");
    }

    private static void WebTest()
    {
      server = new WebServer();
      server.AddBindingAddress("http://localhost:9999/");
      server.RootPath = "c:\\wwwroot";
      server.ActionRequested += server_ActionRequested;
    }

    private static void server_ActionRequested(object sender, ActionRequestedEventArgs e)
    {
      e.Server.WriteDefaultAction(e.Context);
    }
  }
}
