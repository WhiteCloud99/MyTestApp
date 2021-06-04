using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyLib.SimpleWebServer
{
  public class WebServer : IDisposable
  {
    public event EventHandler<ActionRequestedEventArgs> ActionRequested;

    private static Dictionary<string, string> _mimeTypeDictionary = null;


    #region Field 
    /// <summary> 
    /// 청취자 
    /// </summary> 
    private HttpListener listener = null;

    /// <summary> 
    /// 바인딩 주소 리스트 
    /// </summary> 
    private List<string> bindingAddressList = new List<string>();

    /// <summary> 
    /// 루트 경로 
    /// </summary> 
    private string rootPath = null;

    /// <summary> 
    /// 청취 스레드 
    /// </summary> 
    private Thread listenThread = null;

    /// <summary> 
    /// 응답 스레드 리스트 
    /// </summary> 
    private List<Thread> responseThreadList = null;

    /// <summary> 
    /// 실행 여부 
    /// </summary> 
    private bool isRunning = false;

    /// <summary> 
    /// 해제 여부 
    /// </summary> 
    private bool isDisposed = false;

    /// <summary> 
    /// 버퍼 크기 
    /// </summary> 
    private const int BUFFER_SIZE = 4096;

    #endregion

    #region Properties
    public string RootPath
    {
      get { return this.rootPath; }
      set
      {
        if (this.isRunning)
        {
          return;
        }
        this.rootPath = value;
      }
    }

    public bool IsRunning
    {
      get { return this.isRunning; }
    }
    #endregion

    #region 생성자
    static WebServer()
    {
      _mimeTypeDictionary = new Dictionary<string, string>();
      _mimeTypeDictionary.Add(".js", "application/js");
      _mimeTypeDictionary.Add(".json", "application/json");
      _mimeTypeDictionary.Add(".html", "text/html; charset=utf-8");
      _mimeTypeDictionary.Add(".css", "text/css; charset=utf-8");
      _mimeTypeDictionary.Add(".text", "text/text; charset=utf-8");
      _mimeTypeDictionary.Add(".jpg", "image/jpeg");
      _mimeTypeDictionary.Add(".png", "image/png");
    }

    public WebServer()
    {
    }
    #endregion

    #region 바인딩 주소 포함 여부 구하기 - ContainsBindingAddress(bindingAddress)
    /// <summary>
    /// 바인딩 주소 포함여부
    /// </summary>
    /// <param name="bindingAddress">바인딩 주소</param>
    /// <returns>바인딩 주소 포함여부</returns>
    public bool ContainBindingAddress(string bindingAddress)
    {
      return this.bindingAddressList.Contains(bindingAddress);
    }
    #endregion

    #region 바인딩 주소 추가하기
    /// <summary>
    /// 바인딩 주소 추가
    /// </summary>
    /// <param name="bindingAddress"></param>
    public void AddBindingAddress(string bindingAddress)
    {
      if (this.isRunning)
      {
        return;
      }

      if (ContainBindingAddress(bindingAddress))
      {
        return;
      }

      this.bindingAddressList.Add(bindingAddress);
    }
    #endregion

    #region 바인딩 주소 제거
    /// <summary>
    /// 바인딩 주소 제거
    /// </summary>
    /// <param name="bindingAddress"></param>
    public void RemoveBindingAddress(string bindingAddress)
    {
      if (this.isRunning)
      {
        return;
      }

      if (!ContainBindingAddress(bindingAddress))
      {
        return;
      }

      this.bindingAddressList.Remove(bindingAddress);
    }
    #endregion

    #region 바인딩 주소 리스트 삭제
    /// <summary>
    /// 바인딩 주소 리스트 삭제
    /// </summary>
    public void ClearBindingAddressList()
    {
      if (this.isRunning)
      {
        return;
      }

      this.bindingAddressList.Clear();
    }
    #endregion

    #region 서버시작 - Start()
    public void Start()
    {
      if (this.isRunning)
      {
        return;
      }

      if (this.bindingAddressList == null || this.bindingAddressList.Count == 0)
      {
        return;
      }

      this.listener = new HttpListener();

      foreach (string bindingAddress in this.bindingAddressList)
      {
        this.listener.Prefixes.Add(bindingAddress);
      }

      this.listener.AuthenticationSchemes = AuthenticationSchemes.Basic | AuthenticationSchemes.Anonymous;

      this.listener.Start();

      this.isRunning = true;

      if (this.responseThreadList != null)
      {
        foreach (Thread responseThread in this.responseThreadList)
        {
          responseThread.Abort();
        }

        this.responseThreadList.Clear();
      }

      this.responseThreadList = new List<Thread>();

      this.listenThread = new Thread(new ThreadStart(Listen));

      this.listenThread.IsBackground = true;

      this.listenThread.Start();
    }
    #endregion

    #region 서버중단 - Stop()
    public void Stop()
    {
      this.isRunning = false;

      if (this.listenThread != null)
      {
        this.listenThread.Abort();
        this.listenThread = null;
      }

      if (this.responseThreadList != null)
      {
        foreach (Thread responseThread in this.responseThreadList)
        {
          responseThread.Abort();
        }

        this.responseThreadList.Clear();
      }

      if (this.listener != null)
      {
        this.listener.Stop();
        this.listener.Close();
        this.listener = null;
      }
    }
    #endregion


    #region POST - GetPOSTData(request)
    public string GetPOSTData(HttpListenerRequest request)
    {
      if (!request.HasEntityBody)
      {
        return null;
      }

      using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
      {
        return reader.ReadToEnd();
      }
    }
    #endregion

    #region 쿼리문자열 - GetQueryString(request)
    public string GetQueryString(HttpListenerRequest request)
    {
      if (request.QueryString == null || request.QueryString.Count == 0)
      {
        return null;
      }

      StringBuilder sb = new StringBuilder();

      foreach (string key in request.QueryString.AllKeys)
      {
        sb.Append("&");
        sb.AppendFormat("{0}={1}", key, request.QueryString[key]);
      }

      return sb.ToString().TrimStart('&');
    }
    #endregion

    #region 파일쓰기 - WriteFile(request, filePath)
    public void WriteFile(HttpListenerResponse response, string filePath)
    {
      string mimeType = GetMIMEType(filePath);

      response.Headers.Add(HttpResponseHeader.ContentType, mimeType);

      using (BinaryReader reader = new BinaryReader(new FileStream(filePath, FileMode.Open, FileAccess.Read)))
      {
        byte[] byteArray = new byte[BUFFER_SIZE];
        int readCount;

        while ((readCount = reader.Read(byteArray, 0, byteArray.Length)) > 0)
        {
          response.OutputStream.Write(byteArray, 0, readCount);
        }
      }
    }
    #endregion

    #region Write(response, message, addHeader)
    public void Write(HttpListenerResponse response, string message, bool addHeader = false)
    {
      if (addHeader)
      {
        response.Headers.Add(HttpResponseHeader.ContentType, "text/html; charset=utf-8");
      }

      byte[] byteArray = Encoding.UTF8.GetBytes(message);

      response.OutputStream.Write(byteArray, 0, byteArray.Length);
    }
    #endregion

    #region 디폴트 액션쓰기 - WriteDefaultAction(context)
    public void WriteDefaultAction(HttpListenerContext context)
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.AppendFormat("요청 일시 : {0}", DateTime.Now);
      stringBuilder.AppendLine("<br>");
      stringBuilder.AppendFormat("요청 URL : {0}", context.Request.Url);
      stringBuilder.AppendLine("<br>");
      stringBuilder.AppendFormat("액션명 : {0}", context.Request.RawUrl.Substring(0, context.Request.RawUrl.IndexOf("?")));
      stringBuilder.AppendLine("<br>");
      stringBuilder.AppendFormat("요청 종류 : {0}", context.Request.HttpMethod);
      stringBuilder.AppendLine("<br>");
      stringBuilder.AppendFormat("POST DATA : {0}", GetPOSTData(context.Request));
      stringBuilder.AppendLine("<br>");
      stringBuilder.AppendFormat("QUERY STRING : {0}", GetQueryString(context.Request));
      stringBuilder.AppendLine("<br>");

      Write(context.Response, stringBuilder.ToString(), true);
    }
    #endregion

    #region 리소스 해제 - Dispose()
    public void Dispose()
    {
      Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!this.isDisposed)
      {
        if (disposing)
        {
          Stop();
        }

        this.isDisposed = true;
      }
    }
    #endregion

    #region 물리경로 - GetPhysicalPath(logicalPath)
    private string GetPhysicalPath(string logicalPath)
    {
      string physicalPath = logicalPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

      return physicalPath;
    }
    #endregion

    #region MIME 타입 - GetMIMEType(filePath)
    private string GetMIMEType(string filePath)
    {
      string fileExtension = Path.GetExtension(filePath);

      if (_mimeTypeDictionary.ContainsKey(fileExtension))
      {
        return _mimeTypeDictionary[fileExtension];
      }
      else
      {
        return "application/octet-stream";
      }
    }
    #endregion

    #region 디폴트 요청여부 - IsDefaultRequest(request)
    private bool IsDefaultRequest(HttpListenerRequest request)
    {
      bool isDefaultRequest = (request.Url.AbsolutePath == string.Empty || request.Url.AbsolutePath == "/");

      return isDefaultRequest;
    }
    #endregion

    #region 파일 요청여부 - IsFileRequest(request)
    private bool IsFileRequest(HttpListenerRequest request)
    {
      string fileExtension = Path.GetExtension(request.Url.AbsolutePath);

      bool isFileRequest = _mimeTypeDictionary.ContainsKey(fileExtension);

      return isFileRequest;
    }
    #endregion

    #region 액션 요청여부 - isActionRequest(request)
    private bool IsActionRequest(HttpListenerRequest request)
    {
      bool isActionRequest = request.Url.AbsolutePath.EndsWith(".action");

      return isActionRequest;
    }
    #endregion

    #region Listen()
    private void Listen()
    {
      while (this.isRunning)
      {
        HttpListenerContext context = listener.GetContext();

        Thread responseThread = new Thread(new ParameterizedThreadStart(Response));

        responseThread.IsBackground = true;

        this.responseThreadList.Add(responseThread);

        responseThread.Start(new ThreadParameter { Context = context, Thread = responseThread });
      }
    }
    #endregion

    #region Response(parameter)
    private void Response(object parameter)
    {
      ThreadParameter threadParameter = parameter as ThreadParameter;

      HttpListenerContext context = threadParameter.Context;
      HttpListenerRequest request = context.Request;
      HttpListenerResponse response = context.Response;

      try
      {
        if(IsDefaultRequest(request))
        {
          string defaultFilePath = Path.Combine(this.rootPath, "index.html");
          WriteFile(response, defaultFilePath);
        }
        else if(IsFileRequest(request))
        {
          string physicalPath = GetPhysicalPath(request.Url.AbsolutePath);
          string filePath = Path.Combine(this.rootPath, physicalPath);

          if(File.Exists(filePath))
          {
            WriteFile(response, filePath);
          }
          else
          {
            Write(response, string.Format("요청하신 파일이 존재하지 않습니다 : {0}", request.Url.ToString()), true);
          }
        }
        else if(IsActionRequest(request))
        {
          if(ActionRequested != null)
          {
            ActionRequested(this, new ActionRequestedEventArgs(this, context));
          }
          else
          {
            WriteDefaultAction(context);
          }
        }
        else
        {
          Write(response, string.Format("요청하신 URL이 존재하지 않습니다 : {0}", request.Url.ToString()), true);
        }


      }
      catch (Exception ex)
      {
        Write(response, ex.Message + "//" + ex.StackTrace, true);
      }
      finally
      {
        this.responseThreadList.Remove(threadParameter.Thread);
        response.Close();
      }
    }
    #endregion

  }
}
