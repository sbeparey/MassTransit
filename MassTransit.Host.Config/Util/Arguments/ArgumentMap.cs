namespace MassTransit.Host.Config.Util.Arguments
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Reflection;
	using System.Text;

	public class ArgumentMap : IArgumentMap
	{
		private readonly Dictionary<string, ArgumentTarget> _namedArgs = new Dictionary<string, ArgumentTarget>();
		private readonly Type _type;
		private readonly List<ArgumentTarget> _unnamedArgs = new List<ArgumentTarget>();

		public ArgumentMap(Type type)
		{
			_type = type;

			foreach (PropertyInfo property in _type.GetProperties())
			{
				object[] attributes = property.GetCustomAttributes(typeof (ArgumentAttribute), true);
				if (attributes.Length == 1)
				{
					ArgumentAttribute attribute = (ArgumentAttribute) attributes[0];
					DefaultArgumentAttribute defaultAttribute = attribute as DefaultArgumentAttribute;

					ArgumentTarget arg = (defaultAttribute == null)
					                     	? new ArgumentTarget(attribute, property)
					                     	: new DefaultArgumentTarget(attribute, property);

					if (string.IsNullOrEmpty(arg.Attribute.Key))
						_unnamedArgs.Add(arg);
					else
						_namedArgs.Add(arg.Attribute.Key, arg);
				}
			}
		}

		#region IArgumentMap Members

		public string Usage
		{
			get
			{
				StringBuilder sb = new StringBuilder();

				List<ArgumentTarget> allArgs = new List<ArgumentTarget>();
				allArgs.AddRange(_namedArgs.Values);
				allArgs.AddRange(_unnamedArgs);

				foreach (ArgumentTarget target in allArgs)
				{
					if (target is DefaultArgumentTarget)
					{
						sb.AppendFormat("{1}<{0}>{2} ", target.Property.Name, target.Attribute.Required ? "" : "[",
						                target.Attribute.Required ? "" : "]");
					}
					else if (target.Attribute.Required)
						sb.AppendFormat("-{0} ", target.Attribute.Key);
					else
						sb.AppendFormat("[-{0}] ", target.Attribute.Key);
				}

				sb.Append(Environment.NewLine + Environment.NewLine);

				foreach (ArgumentTarget target in allArgs)
				{
					if (target is DefaultArgumentTarget)
					{
						sb.AppendFormat("{0,-20}{1}" + Environment.NewLine, "<" + target.Property.Name + ">",
						                target.Attribute.Description);
					}
					else
					{
						sb.AppendFormat("{0,-20}{1}" + Environment.NewLine, target.Attribute.Key,
						                target.Attribute.Description);
					}
				}

				return sb.ToString();
			}
		}

		#endregion

		public IEnumerable<IArgument> ApplyTo(object obj, IEnumerable<IArgument> arguments, ArgumentIntercepter intercepter)
		{
			List<ArgumentTarget> usedTargets = new List<ArgumentTarget>();
			foreach (KeyValuePair<string, ArgumentTarget> namedArg in _namedArgs)
			{
				usedTargets.Add(namedArg.Value);
			}
			foreach (ArgumentTarget unnamedArg in _unnamedArgs)
			{
				usedTargets.Add(unnamedArg);
			}

			List<IArgument> unused = new List<IArgument>();

			int unnamedIndex = 0;

			foreach (IArgument arg in arguments)
			{
				if (string.IsNullOrEmpty(arg.Key))
				{
					if (unnamedIndex < _unnamedArgs.Count && intercepter(_unnamedArgs[unnamedIndex++].Property.Name, arg.Value))
					{
						usedTargets.Remove(_unnamedArgs[unnamedIndex]);
						ApplyValueToProperty(_unnamedArgs[unnamedIndex++].Property, obj, arg.Value);
					}
					else
					{
						unused.Add(arg);
					}
				}
				else if (_namedArgs.ContainsKey(arg.Key) && intercepter(_namedArgs[arg.Key].Property.Name, arg.Value))
				{
					usedTargets.Remove(_namedArgs[arg.Key]);
					ApplyValueToProperty(_namedArgs[arg.Key].Property, obj, arg.Value);
				}
				else
				{
					unused.Add(arg);
				}
			}

			foreach (ArgumentTarget target in usedTargets)
			{
				if (target.Attribute.Required)
					throw new ArgumentException("Argument " + (target.Attribute.Key ?? target.Property.Name) + " is required");
			}

			return unused;
		}

		public IEnumerable<IArgument> ApplyTo(object obj, IEnumerable<IArgument> arguments)
		{
			return ApplyTo(obj, arguments, delegate { return true; });
		}

		public void ApplyValueToProperty(PropertyInfo property, object obj, string argumentValue)
		{
			object value;

			if (property.PropertyType == typeof (bool))
			{
				value = bool.Parse(argumentValue);
			}
			else
			{
				value = argumentValue;
			}

			property.SetValue(obj, value, BindingFlags.Default, null, null, CultureInfo.InvariantCulture);
		}
	}
}