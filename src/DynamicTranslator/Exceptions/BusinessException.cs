﻿using System;

namespace DynamicTranslator.Exceptions
{
    public class BusinessException : Exception
    {
        public BusinessException(string message, Exception ex, object[] messageParameters) : base(message, ex)
        {
            MessageParameters = messageParameters;
        }

        public BusinessException(string message, Exception ex) : base(message, ex) {}

        public BusinessException(string message) : base(message) {}

        public object[] MessageParameters { get; set; }

        public string ResultMessage { get; set; }
    }
}
