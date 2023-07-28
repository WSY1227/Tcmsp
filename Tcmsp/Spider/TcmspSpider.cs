using System.Text.RegularExpressions;
using DotnetSpider;
using DotnetSpider.DataFlow;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.Http;
using DotnetSpider.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Tcmsp.Spider.Item;

namespace Tcmsp.Spider;

public class TcmspSpider : DotnetSpider.Spider
{
    public TcmspSpider(IOptions<SpiderOptions> options, DependenceServices services, ILogger<DotnetSpider.Spider> logger) : base(options, services, logger)
    {
    }

    protected override async Task InitializeAsync(CancellationToken stoppingToken = new CancellationToken())
    {
        // 添加自定义解析
        AddDataFlow(new Parser());
        // 使用控制台存储器
        AddDataFlow(new ConsoleStorage());
        // 添加采集请求
        await AddRequestsAsync(new Request("https://old.tcmsp-e.com/tcmspsearch.php?qs=herb_all_name&q=%E9%99%88%E7%9A%AE")
        {
            // 请求超时 10 秒
            Timeout = 10000
        });
    }
    protected override SpiderId GenerateSpiderId()
    {
        return new(ObjectId.CreateId().ToString(), "Github");
    }


    private class Parser : DataParser
    {
        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task ParseAsync(DataFlowContext context)
        {
            var selectable = context.Selectable;
            
            // 获取请求链接
            var reqUrl = context.Request.RequestUri.ToString();
            
            // 取第一个input[type='hidden'][name='token']的值
            var token = selectable.XPath("//input[@type='hidden' and @name='token']/@value")
                ?.Value;
            //判断reqUrl是否包含token
            if (reqUrl.Contains("token"))
            {
                

                if (reqUrl.Contains("herb_all_name"))
                {
                    // 拿到第一个id为kendoResult内的第一个script标签的内容
                    var script = selectable.XPath("//div[@id='kendoResult']/script[1]")
                        ?.Value;
                    const string pattern = @"data*:\s(\[[^\]]*\])";
                    if (script == null) return Task.CompletedTask;
                    var match = Regex.Match(script, pattern);
                    if (match.Success)
                    {
                        string dataJson = match.Groups[1].Value;
                        // 现在你有了包含数据的 JSON 字符串。
                        // 让我们将其转换为动态对象的列表。
                        var dataObjects = JsonConvert.DeserializeObject<dynamic>(dataJson);
                        if (dataObjects == null) return Task.CompletedTask;
                        foreach (var dataObject in dataObjects)
                        {
                            // 获取每个对象的各个属性
                            string herbCnName = dataObject.herb_cn_name;
                            string herbEnName = dataObject.herb_en_name;
                            string herbPinyin = dataObject.herb_pinyin;

                            Console.WriteLine($"中文名: {herbCnName}, 英文名: {herbEnName}, 中文拼音名: {herbPinyin}");
                            var url =
                                $"https://old.tcmsp-e.com/tcmspsearch.php?qr={herbEnName}&qsr=herb_en_name&token={token}";
                            context.AddFollowRequests(new Request(url));
                        }
                    }
                    else
                    {
                        Console.WriteLine("未在 JavaScript 代码中找到数据。");
                    }
                }
                else
                {
                    var script = selectable.XPath("//*[@id='kendoResult']//*[@id='tabstrip']/script[2]")?.Value;
                    if (script == null) return Task.CompletedTask;
                    var jsonMatches = ExtractJsonData(script);
                   
                   var ingredients = JsonConvert.DeserializeObject<List<Ingredients>>(jsonMatches[0]);
                   var relatedTargets = JsonConvert.DeserializeObject<List<RelatedTargets>>(jsonMatches[0]);
                   
                   Console.WriteLine(ingredients);
                   Console.WriteLine(relatedTargets);
                   
                }
              
                // context.AddData("author", author);
                // context.AddData("username", name);
            }
            else
            {
                var url =
                    $"https://old.tcmsp-e.com/tcmspsearch.php?qs=herb_all_name&q=%E9%99%88%E7%9A%AE&token={token}";
                context.AddFollowRequests(new Request(url));
            }
            return Task.CompletedTask;
        }
        static List<string> ExtractJsonData(string input)
        {
            List<string> jsonDataList = new List<string>();
            string pattern = @"data:\s*(\[.*?\])\,";
            // string pattern = @"(?<=data: ).*?(?=}],)";
            MatchCollection matches = Regex.Matches(input, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                jsonDataList.Add(match.Groups[1].Value);
            }

            return jsonDataList;
        }
    }
}