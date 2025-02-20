// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using PSRule.Configuration;
using PSRule.Data;

namespace PSRule.Pipeline
{
    public interface IGetTargetPipelineBuilder : IPipelineBuilder
    {
        void InputPath(string[] path);
    }

    /// <summary>
    /// A helper to construct the pipeline for Assert-PSRule.
    /// </summary>
    internal sealed class GetTargetPipelineBuilder : PipelineBuilderBase, IGetTargetPipelineBuilder
    {
        private InputFileInfo[] _InputPath;

        internal GetTargetPipelineBuilder(Source[] source, IHostContext hostContext)
            : base(source, hostContext)
        {
            _InputPath = null;
        }

        public override IPipelineBuilder Configure(PSRuleOption option)
        {
            if (option == null)
                return this;

            base.Configure(option);

            Option.Output = new OutputOption();
            Option.Output.Culture = GetCulture(option.Output.Culture);

            ConfigureBinding(option);
            Option.Requires = new RequiresOption(option.Requires);

            return this;
        }

        public void InputPath(string[] path)
        {
            if (path == null || path.Length == 0)
                return;

            var builder = new InputPathBuilder(GetOutput(), PSRuleOption.GetWorkingPath(), "*", GetInputFilter());
            builder.Add(path);
            _InputPath = builder.Build();
        }

        public override IPipeline Build(IPipelineWriter writer = null)
        {
            return new GetTargetPipeline(PrepareContext(null, null, null), PrepareReader(), writer ?? PrepareWriter());
        }

        protected override PipelineReader PrepareReader()
        {
            if (!string.IsNullOrEmpty(Option.Input.ObjectPath))
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.ReadObjectPath(sourceObject, next, Option.Input.ObjectPath, true);
                });
            }

            if (Option.Input.Format == InputFormat.Yaml)
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.ConvertFromYaml(sourceObject, next);
                });
            }
            else if (Option.Input.Format == InputFormat.Json)
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.ConvertFromJson(sourceObject, next);
                });
            }
            else if (Option.Input.Format == InputFormat.Markdown)
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.ConvertFromMarkdown(sourceObject, next);
                });
            }
            else if (Option.Input.Format == InputFormat.PowerShellData)
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.ConvertFromPowerShellData(sourceObject, next);
                });
            }
            else if (Option.Input.Format == InputFormat.File)
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.ConvertFromGitHead(sourceObject, next);
                });
            }
            else if (Option.Input.Format == InputFormat.Detect && _InputPath != null)
            {
                AddVisitTargetObjectAction((sourceObject, next) =>
                {
                    return PipelineReceiverActions.DetectInputFormat(sourceObject, next);
                });
            }
            return new PipelineReader(VisitTargetObject, _InputPath, GetInputObjectSourceFilter());
        }
    }

    /// <summary>
    /// A pipeline that gets target objects through the pipeline.
    /// </summary>
    internal sealed class GetTargetPipeline : RulePipeline
    {
        internal GetTargetPipeline(PipelineContext context, PipelineReader reader, IPipelineWriter writer)
            : base(context, null, reader, writer) { }

        public override void Process(PSObject sourceObject)
        {
            try
            {
                Reader.Enqueue(sourceObject);
                while (Reader.TryDequeue(out var next))
                    Writer.WriteObject(next.Value, false);
            }
            catch (Exception)
            {
                End();
                throw;
            }
        }
    }
}
