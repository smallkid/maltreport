﻿//作者：李维
//创建时间：2010-09-03

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Collections;
using System.Security;
using System.Xml;
using System.Diagnostics;
using System.Globalization;

using Bravo.Reporting.Xml;

namespace Bravo.Reporting.Office2003Xml
{
    public class ExcelXmlTemplate : ExcelXmlDocument, ITemplate
    {
        private ITextTemplateEngine engine;

        public ExcelXmlTemplate()
        {
            this.engine = new VelocityTextTemplateEngine("ExcelXmlTemplate");
            this.engine.RegisterFilter(typeof(string), new XmlStringRenderFilter());
        }

        #region ITemplate 接口实现

        public IDocument Render(IDictionary<string, object> context)
        {
            Debug.Assert(this.engine != null);

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var resultDocument = (ExcelXmlDocument)this.Clone();

            //执行主要内容的渲染过程
            using (var inStream = this.GetInputStream())
            using (var reader = new StreamReader(inStream, Encoding.UTF8))
            using (var ws = new MemoryStream())
            using (var writer = new StreamWriter(ws))
            {
                //执行渲染
                this.engine.Evaluate(context, reader, writer);
                resultDocument.PutBuffer(ws.GetBuffer());
            }

            return resultDocument;
        }

        #endregion

        internal void LoadFromDocument(ExcelXmlDocument doc)
        {
            var buf = doc.GetBuffer();
            var newBuf = new byte[buf.Length];
            Buffer.BlockCopy(buf, 0, newBuf, 0, buf.Length);
            this.PutBuffer(newBuf);
        }

        public override ITemplate Compile()
        {
            throw new NotSupportedException();
        }

    }
}