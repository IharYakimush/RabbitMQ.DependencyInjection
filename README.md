# RabbitMQ.DependencyInjection
###1. Register one or more RabbitMq connections. 
Use type params to distinguish different instances. By default connection has recommended lifetime Singleton, but it can be configured differently.
###2. Register one or more RabbitMq models. 
Model registration required 2 type params. First to be able to inject different models to your classes. Second to bind model to connection. Define bootstrap action that will be executed after new model creation. Usefull for declaring exchanges, queues, etc.

```
static void Main(string[] args)
{
    new HostBuilder()
    .ConfigureLogging((ILoggingBuilder b) =>
    {
        b.AddConsole();
        b.SetMinimumLevel(LogLevel.Debug);
    })
    .ConfigureServices(services =>
    {
        services.AddRabbitMqConnection<RabbitMqSetup.Connection1>((s, f) =>
        {
            f.ClientProvidedName = "sample1";
            f.Endpoint = new AmqpTcpEndpoint("localhost", 5672);
            f.UserName = "myUser";
            f.Password = "myPass";
            f.DispatchConsumersAsync = true;
        });

        services.AddRabbitMqModel<RabbitMqSetup.Exc1, RabbitMqSetup.Connection1>((s, m) =>
        {
            m.ExchangeDeclare(RabbitMqSetup.Exc1.Name, ExchangeType.Topic, false, true, new Dictionary<string, object>());
        });

        services.AddRabbitMqModel<RabbitMqSetup.Queue1, RabbitMqSetup.Connection1>((s, m) =>
        {
            m.QueueDeclare(RabbitMqSetup.Queue1.Name);
            m.QueueBind(RabbitMqSetup.Queue1.Name, RabbitMqSetup.Exc1.Name, "#");                        
        });

        services.AddHostedService<Producer>();
        services.AddHostedService<Consumer>();

    }).Build().Run();
}
```

```
public static class RabbitMqSetup
{
    public class Exc1
    {
        public const string Name = "myExc";
    }

    public class Connection1
    {
    }

    public class Queue1
    {
        public const string Name = "myQueue";
    }
}
```
###3. First option of model usage
Inject `RabbitMqModelsObjectPool<TModel>` class. It is an ObjectPool that can be used to get and return IModel instance. It is created with same service lifetime as connection. Sample of message producer with this approach:
```
class Producer : BackgroundService
{
    private readonly RabbitMqModelsObjectPool<RabbitMqSetup.Exc1> excObjectPool;
    private readonly ILogger<Producer> logger;

    public Producer(RabbitMqModelsObjectPool<RabbitMqSetup.Exc1> excObjectPool, ILogger<Producer> logger)
    {
        this.excObjectPool = excObjectPool ?? throw new ArgumentNullException(nameof(excObjectPool));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            string value = Guid.NewGuid().ToString();

            IModel model = null;
            try
            {
                model = this.excObjectPool.Get();
                model.BasicPublish(RabbitMqSetup.Exc1.Name, "routingKey", false, null, Encoding.UTF8.GetBytes(value));

                this.logger.LogInformation("Published {value}", value);
            }
            finally
            {
                this.excObjectPool.Return(model);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }
}
```
###4. Second option of model usage
Inject `IRabbitMqModel<TModel>` interface. It is registered in container with Transient lifetime and when needed created from same ObjectPool described in section 3. Don't dispose model in your code to allow it returning to object pool automatically. Sample of message consumer with this approach:
```
class Consumer : BackgroundService
{
    private readonly IRabbitMqModel<RabbitMqSetup.Queue1> queue;
    private readonly ILogger<Consumer> logger;

    public Consumer(IRabbitMqModel<RabbitMqSetup.Queue1> queue, ILogger<Consumer> logger)
    {
        this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(this.queue.Model);
        consumer.Received += ConsumerReceived;
        string tag = this.queue.Model.BasicConsume(RabbitMqSetup.Queue1.Name, true, consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!this.queue.Model.IsOpen)
            {
                throw new Exception();
            }

            await Task.Delay(1000);
        }

        this.queue.Model.BasicCancelNoWait(tag);
    }

    private Task ConsumerReceived(object sender, BasicDeliverEventArgs msg)
    {
        this.logger.LogInformation("Recieved {value}", Encoding.UTF8.GetString(msg.Body.ToArray()));

        return Task.CompletedTask;
    }
}
```
# Logging
If `ILoggerFactory` available in container following events will be logged. You can change default logging category and events level using `RabbitMq.DependencyInjection.Logging` class.
###Default events
Catgory | Event Name | Log Level | Comments
--- | --- | ---
RabbitMq.Connection | ConnectionCreated | Information | - |
RabbitMq.Connection | ConnectionBlocked | Information | - |

### Console output sample:
```
info: RabbitMq.Connection[101]
      Connection sample1 of type Sample.RabbitMqSetup+Connection1 created
dbug: RabbitMq.Model[201]
      Model of type Sample.RabbitMqSetup+Exc1 created
info: Sample.Producer[0]
      Published 852ec9f7-437b-4650-8fff-2f179e36cd9b
dbug: RabbitMq.Model[202]
      Model of type Sample.RabbitMqSetup+Exc1 return to ObjectPool True
dbug: RabbitMq.Model[201]
      Model of type Sample.RabbitMqSetup+Queue1 created
info: Sample.Producer[0]
      Published 269dd37b-ec9f-4f94-8071-5c4c4f7b1905
dbug: RabbitMq.Model[202]
      Model of type Sample.RabbitMqSetup+Exc1 return to ObjectPool True
info: Sample.Consumer[0]
      Recieved 269dd37b-ec9f-4f94-8071-5c4c4f7b1905
info: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
dbug: RabbitMq.Model[202]
      Model of type Sample.RabbitMqSetup+Queue1 return to ObjectPool True
info: RabbitMq.Connection[104]
      Connection sample1 of type Sample.RabbitMqSetup+Connection1 shutdown. Application 200 Connection close forced (null)
```
#NuGet