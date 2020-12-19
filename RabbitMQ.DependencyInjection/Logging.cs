﻿using Microsoft.Extensions.Logging;

namespace RabbitMQ.DependencyInjection
{
    public static class Logging
    {
        public static class Connection
        {
            public static string CategoryName = "RabbitMQ.Connection";

            public static EventId CreatedEventId = new EventId(101, "ConnectionCreated");
            public static LogLevel CreatedEventLevel = LogLevel.Information;

            public static EventId BlockedEventId = new EventId(102, "ConnectionBlocked");
            public static LogLevel BlockedEventLevel = LogLevel.Warning;

            public static EventId UnblockedEventId = new EventId(103, "ConnectionUnblocked");
            public static LogLevel UnblockedEventLevel = LogLevel.Information;

            public static EventId ShutdownEventId = new EventId(104, "ConnectionShutdown");
            public static LogLevel ShutdownEventLevel = LogLevel.Information;
        }

        public static class Model
        {
            public static string CategoryName = "RabbitMQ.Model";

            public static EventId CreatedEventId = new EventId(201, "ModelCreated");
            public static LogLevel CreatedEventLevel = LogLevel.Debug;

            public static EventId ReturnEventId = new EventId(202, "ModelReturn");
            public static LogLevel ReturnEventLevel = LogLevel.Debug;

            public static EventId BootstrapErrorEventId = new EventId(203, "ModelBootstrapError");
            public static LogLevel BootstrapErrorEventLevel = LogLevel.Error;
        }
    }
}
