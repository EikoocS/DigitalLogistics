using DigitalLogistics.Storage.Items;
using System;
using System.Globalization;
using System.Linq.Expressions;
using DigitalLogistics.Filtering;
using DigitalLogistics.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DigitalLogistics.UI
{
    internal sealed class SearchQueryDebugView
    {
        private readonly CustomUIBuilder output;
        private StoredItemExpression expression = StoredItemExpression.Constant(true);
        private string parseError;
        private GameItem sample;

        internal SearchQueryDebugView(CustomUIBuilder builder, Func<string, StoredItemExpression> parse, string inputLabel)
        {
            builder.BeginRow(alignment: TextAnchor.MiddleLeft);
            builder.AddButton("", Refresh);
            var sampleButton = builder.Last;
            sampleButton.SetPreferredSize(40, 40).SetFixedWidth(40).SetFlexible(0, 0);
            sampleButton.button.gameObject.AddComponent<SearchQuerySamplePicker>().Sampled = item =>
            {
                sample = item;
                Refresh();
            };
            builder.AddInput(inputLabel, onChanged: text =>
            {
                try
                {
                    expression = parse(text);
                    parseError = null;
                }
                catch (Superpower.ParseException error)
                {
                    expression = null;
                    parseError = error.Message;
                }
                Refresh();
            });
            builder.Last.input.lineType = TMP_InputField.LineType.SingleLine;
            builder.Last.SetPreferredSize(-1, 40).SetFlexible(1, 0);
            builder.End();
            builder.BeginScrollFlexible();
            output = new CustomUIBuilder(CustomUIManager.Instance, builder.Handle, builder.CurrentContainer);
            builder.End();
            Refresh();
        }

        private void Refresh()
        {
            CustomUIBuilder.ClearContainer(output.CurrentContainer);
            CachedItem item = sample == null ? null : new PhysicalItem(sample);
            if (item != null)
            {
                output.AddLabel(item.DisplayName);
                output.Last.text.richText = false;
            }
            if (parseError != null)
            {
                output.AddLabel(parseError);
                output.Last.text.richText = false;
                output.Last.text.color = MachineryStyle.PROGRESS_FILL_DANGER;
            }
            else AddNode(expression.Tree.Body, item, 0);
            CustomUIBuilder.RebuildLayout(output.CurrentContainer);
        }

        private void AddNode(Expression node, CachedItem item, int depth)
        {
            string text = node switch
            {
                BinaryExpression => node.NodeType switch
                {
                    ExpressionType.AndAlso => "AndAlso (&&)",
                    ExpressionType.OrElse => "OrElse (||)",
                    _ => node.NodeType.ToString()
                },
                UnaryExpression => node.NodeType.ToString(),
                MethodCallExpression call => call.Method.Name,
                _ => node.ToString()
            };
            object value = null;
            if (item != null)
            {
                value = Expression.Lambda<Func<CachedItem, object>>(
                    Expression.Convert(node, typeof(object)), expression.Tree.Parameters).Compile()(item);
                text += " = " + FormatValue(value);
            }

            output.AddLabel(text);
            var label = output.Last;
            label.text.richText = false;
            label.text.enableAutoSizing = false;
            label.text.enableWordWrapping = true;
            label.text.overflowMode = TextOverflowModes.Overflow;
            label.text.alignment = TextAlignmentOptions.TopLeft;
            label.text.margin = new Vector4(depth * 24, 0, 0, 0);
            if (value is bool result)
                label.text.color = result ? MachineryStyle.PROGRESS_FILL : MachineryStyle.PROGRESS_FILL_DANGER;
            var layout = label.root.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = layout.preferredHeight = -1;
                layout.flexibleHeight = 0;
            }

            switch (node)
            {
                case BinaryExpression binary:
                    AddNode(binary.Left, item, depth + 1);
                    AddNode(binary.Right, item, depth + 1);
                    break;
                case UnaryExpression unary:
                    AddNode(unary.Operand, item, depth + 1);
                    break;
                case MethodCallExpression call:
                    if (call.Object != null) AddNode(call.Object, item, depth + 1);
                    foreach (var argument in call.Arguments) AddNode(argument, item, depth + 1);
                    break;
            }
        }

        private static string FormatValue(object value) => value switch
        {
            null => "null",
            string text => "\"" + text + "\"",
            bool result => result ? "true" : "false",
            CachedItem item => item.DisplayName,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
    }
}
