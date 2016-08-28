﻿
using System;
using System.Collections.Generic;
using DB=DatabaseFramework;
using TSF.UmlToolingFramework.Wrappers.EA;
using System.Linq;

namespace EAAddinFramework.Databases
{
	/// <summary>
	/// Description of Constraint.
	/// </summary>
	public abstract class Constraint:DatabaseItem,DB.Constraint
	{
		internal Operation _wrappedOperation;
		internal Table _owner;
		internal string _name;
		private List<Column> _involvedColumns;
		public Constraint(Table owner,Operation operation)
		{
			_owner = owner;
			_wrappedOperation = operation;
		}
		public Constraint(Table owner, List<Column> involvedColumns)
		{
			_owner = owner;
			_involvedColumns = involvedColumns;
			this.owner.addConstraint(this);
		}

		#region implemented abstract members of DatabaseItem
		internal abstract override void createTraceTaggedValue();
		internal override Element wrappedElement {
			get {
				return this._wrappedOperation;
			}
			set {
				this._wrappedOperation = (Operation)value;
			}
		}
		#endregion
		#region Constraint implementation

		public string name 
		{
			get 
			{
				if (_wrappedOperation != null)
				{
					return _wrappedOperation.name;
				}
				else
				{
					return _name;
				}
			}
			set 
			{
				if (_wrappedOperation != null)
				{
					_wrappedOperation.name = value;
				}
				this._name = value;
			}
		}

		public DB.Table owner 
		{
			get 
			{
				return _owner;
			}
			set 
			{
				throw new NotImplementedException();
			}
		}

		public virtual string itemType 
		{
			get {return "Constraint";}
		}


		public virtual string properties 
		{
			get 
			{
				string _properties = string.Empty;
				if (this.involvedColumns.Count > 0)
				{
					_properties += " (" 
						+ string.Join(", ",this.involvedColumns.Select( x => x.name).ToArray().OrderBy(x => x.ToString()))
						+ ")";
				}
				return _properties;
			}
		}

		public List<DB.Column> involvedColumns 
		{
			get 
			{
				if (_involvedColumns == null)
				{
					this.getInvolvedColumns();
				}
				return _involvedColumns.Cast<DB.Column>().ToList();
			}
			set 
			{
				_involvedColumns = value.Cast<Column>().ToList();
				if (this._wrappedOperation == null)
				{
					List<Parameter> parameters = new List<Parameter>();
					foreach (var column in _involvedColumns) 
					{
						{
							Parameter parameter = this._wrappedOperation.model.factory.createNewElement<Parameter>(this._wrappedOperation, column.name);
							parameter.type = ((Column)column)._wrappedattribute.type;
						}
					}
				}
			}
		}
		private void getInvolvedColumns()
		{
			_involvedColumns = new List<Column>();
			if (this._wrappedOperation != null)
			{
				foreach (var parameter in this._wrappedOperation.ownedParameters) 
				{
					Column involvedColumn = _owner.columns.FirstOrDefault(x => x.name == parameter.name) as Column;
					if (involvedColumn != null)
					{
						_involvedColumns.Add(involvedColumn);
					}
				}
			}
		}
		#endregion

	}
}
