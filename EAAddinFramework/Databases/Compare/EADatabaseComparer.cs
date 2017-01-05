﻿
using System;
using System.Collections.Generic;
using System.Linq;
using TSF.UmlToolingFramework.UML.Classes.Kernel;
using UML=TSF.UmlToolingFramework.UML;
using UTF_EA=TSF.UmlToolingFramework.Wrappers.EA;
using DB=DatabaseFramework;
using DB_EA = EAAddinFramework.Databases;
using EAAddinFramework.Utilities;
using DatabaseFramework.Compare;

namespace EAAddinFramework.Databases.Compare
{
	/// <summary>
	/// Description of EADatabaseComparer.
	/// </summary>
	public class EADatabaseComparer:DatabaseComparer
	{
		internal Database _newDatabase;
		internal Database _existingDatabase;
		List<Column> deletedNewColumns = new List<Column>();
		public EADatabaseComparer(Database newDatabase, Database existingDatabase)
		{
			this._newDatabase = newDatabase;
			this._existingDatabase = existingDatabase;
		}
		#region DatabaseComparer implementation
		public void save()
		{
			//both existing and new database should exist to even begin
			if (_existingDatabase == null || _newDatabase == null) throw new Exception("Both existing and new database should exist in order to save the database!");
			//first save all valid tables
			var tableComparers = this.comparedItems.Where(x =>x.itemType.Equals("Table",StringComparison.InvariantCultureIgnoreCase)
			                                              && (x.newDatabaseItem == null
			                                                    ||  x.newDatabaseItem.isValid ));
			foreach (var tableCompareItem in tableComparers )
			{
				tableCompareItem.save(_existingDatabase);
			}
			//loop each valid table again and save its columns, setting the correct order
			foreach (var tableCompareItem in tableComparers )
			{
				//then save all columns
				int i = 1;
				foreach (var columnCompareItem in tableCompareItem.ownedComparisons.Where(
					x => x.itemType.Equals("Column",StringComparison.InvariantCultureIgnoreCase)))
				{
					columnCompareItem.updatePosition(i);
					columnCompareItem.save(_existingDatabase);
					i++;
				}
			}

			//then save all primary keys
			foreach (var PKCompareItem in this.comparedItems.Where(x => x.itemType.Equals("Primary Key",StringComparison.InvariantCultureIgnoreCase)
			                                                                              &&( x.newDatabaseItem == null || (x.newDatabaseItem.isValid && x.newDatabaseItem.owner.isValid))))
			{
				PKCompareItem.save(_existingDatabase);
			}
			//then save all foreign keys
			foreach (var FKCompareItem in this.comparedItems.Where(x => x.itemType.Equals("Foreign Key",StringComparison.InvariantCultureIgnoreCase)
			                                                                              &&( x.newDatabaseItem == null || (x.newDatabaseItem.isValid && x.newDatabaseItem.owner.isValid))))
			{
				FKCompareItem.save(_existingDatabase);
			}

			//refresh model in gui
			this._existingDatabase._wrappedPackage.refresh();
		}

		public void compare()
		{
			this.comparedItems = new List<DatabaseItemComparison>();
			var tableComparisons = new List<EADatabaseItemComparison>();
			//compare each new table
			if (newDatabase != null)
			{
				foreach (var newTable in _newDatabase.tables)
				{
					Table existingTable = null;
					if (_existingDatabase != null) existingTable = (Table)_existingDatabase.getCorrespondingTable(newTable);
					tableComparisons.Add( new EADatabaseItemComparison(newTable,existingTable));
				}
			}
			if (existingDatabase != null)
			{
				//get existing tables that don't exist in the new database
				foreach (var existingTable in _existingDatabase.tables)
				{
					//if the existingTable does not exist in the comparedItems then add it.
					if (! tableComparisons.Any(x => x.existingDatabaseItem == existingTable))
					{
						//maybe this table is derived from the same logical class as another table 
						// in that case we add the oter table as new item
						Table newTable = null;
						if (_newDatabase != null) newTable = (Table)_newDatabase.getCorrespondingTable(existingTable);
						tableComparisons.Add(new EADatabaseItemComparison(newTable,existingTable));
					}
				}
			}
			//first process the ones that have an existing database item
			foreach (var tableComparison in tableComparisons.Where(x => x.existingDatabaseItem != null).OrderBy(y => y.existingDatabaseItem.position).ThenBy(y => y.existingDatabaseItem.name))
			{
				addTableComparison(tableComparison);
			}
			//then the ones without an existing database item
			foreach (var tableComparison in tableComparisons.Where(x => x.existingDatabaseItem == null).OrderBy(y => y.newDatabaseItem.position).ThenBy(y => y.newDatabaseItem.name))
			{
				addTableComparison(tableComparison);
			}
			//check and update overriden items
			this.updateOverrides(comparedItems);
		}
		private void addTableComparison(EADatabaseItemComparison tableComparison)
		{
			addToComparison(tableComparison);
			var tableComparisons = this.addComparisonDetails(tableComparison);
			//add them to the comparison
			addToComparison(tableComparisons);
		}
		/// <summary>
		/// for each overridden existing item we update the new item, but only if this is the only existing item  referencing the new item.
		/// If there are more then we need to duplicate the columns in the new database
		/// </summary>
		public void updateOverrides(List<DatabaseItemComparison> tableComparisons )
		{
			List<DB.DatabaseItem> updatedNewItems = new List<DB.DatabaseItem>();
			//get all elements that are overridden on the existing table side
			foreach (var overrideCompare in tableComparisons.Where(x => x.existingDatabaseItem != null
			                                                      && x.existingDatabaseItem.isOverridden))
			{
				this.setOverride(overrideCompare,true, updatedNewItems);
			} 
		}
		public void setOverride(DatabaseItemComparison overrideCompare, bool overrideValue,List<DB.DatabaseItem>updatedNewItems = null)
		{
			if (overrideValue)
			{
				//set the comparisonstatus just in case
				overrideCompare.comparisonStatus = DatabaseComparisonStatusEnum.dboverride;
				if (overrideCompare.existingDatabaseItem != null)
				{
					overrideCompare.existingDatabaseItem.isOverridden = true;
				}
				else
				{
					overrideCompare.newDatabaseItem.isOverridden = true;
				}
			}
			else
			{
				//un-override item
				// existing item
				if (overrideCompare.existingDatabaseItem != null 
				    && overrideCompare.existingDatabaseItem.isOverridden) 
						overrideCompare.existingDatabaseItem.isOverridden = false;
				// new item
				if (overrideCompare.newDatabaseItem != null 
				    && overrideCompare.newDatabaseItem.isOverridden) 
						overrideCompare.newDatabaseItem.isOverridden = false;
			}
			
		}
		private void addToComparison(DatabaseItemComparison comparedItem)
		{
			//only add the commparison if not both of them are null
			if (comparedItem.existingDatabaseItem != null 
			    || comparedItem.newDatabaseItem != null)
			{
				comparedItems.Add(comparedItem);
			}
		}
		private void addToComparison(List<DatabaseItemComparison> addedComparedItems)
		{
			foreach (var comparedItem in addedComparedItems) 
			{
				addToComparison( comparedItem);
			}
		}
		public List<DatabaseItemComparison> addComparisonDetails(EADatabaseItemComparison comparedItem)
		{
			List<DatabaseItemComparison> addedComparedItems = new List<DatabaseItemComparison>();
			var existingTable = comparedItem.existingDatabaseItem as Table;
			var newTable = comparedItem.newDatabaseItem as Table;
			if (existingTable != null)
			{
				List<Column> alreadMappedcolumns = new List<Column>();
				//add all existing columns
				foreach (Column existingColumn in existingTable.columns.OrderBy(x => x.position))
				{
					Column newColumn = null;
					if (newTable != null) 
					{
						newColumn = newTable.getCorrespondingColumn(existingColumn,alreadMappedcolumns);
					}
					if (newColumn != null)
					{
						alreadMappedcolumns.Add(newColumn);
					}
					addedComparedItems.Add(comparedItem.addOwnedComparison(newColumn,existingColumn));
				}
				//now the new columns that don't have a corresponding existing column
				if (newTable != null)
				{
					List<Column> columnsToDelete = new List<Column>();
					foreach (Column newColumn in newTable.columns) 
					{
						//if not already mapped
						if (addedComparedItems.All(x => x.newDatabaseItem != newColumn)) 
						{
							//and there is no other column with the same name that maps to the same physical column and has exactly the same properties and name
							var newColumnsToDelete = addedComparedItems.Where(y =>
														y.existingDatabaseItem is DB_EA.Column														
							                            && y.existingDatabaseItem.logicalElements.Contains(newColumn.logicalElement)
							                            && y.newDatabaseItem is DB_EA.Column	
							                            && y.newDatabaseItem.name == newColumn.name
							                            && ((Column)y.newDatabaseItem).type.name == newColumn.type.name)
														.Select(z => z.newDatabaseItem as Column);
							if (newColumnsToDelete.Any()) 
							{
								columnsToDelete.AddRange(newColumnsToDelete);
							} 
							else
							{
								//no duplicates so add to the comparison						    	
								addedComparedItems.Add(comparedItem.addOwnedComparison(newColumn, null));
							}
						}
					}
					//add the deleted columns to the list of deleted columns
					this.deletedNewColumns.AddRange(columnsToDelete);
					//delete the columns that were identified as duplicates
					foreach (var columnToDelete in columnsToDelete) 
					{
						columnToDelete.delete();
					}
				}
				//add all existing constraints
				foreach (var existingConstraint in existingTable.constraints) 
				{
					Constraint newConstraint = null;
					if (newTable != null)
					{
						if (existingConstraint is ForeignKey)
						{
							newConstraint = newTable.getCorrespondingForeignKey((ForeignKey)existingConstraint);
						}
						else
						{
							newConstraint = (Constraint)newTable.primaryKey ;
						}
						addedComparedItems.Add(comparedItem.addOwnedComparison(newConstraint,existingConstraint));
					}
				}
				//then add the new constraints don't have a corresponding existing constraint
				if (newTable != null)
				{
					foreach (var newConstraint in newTable.constraints) 
					{
						if (! addedComparedItems.Any(x => x.newDatabaseItem == newConstraint))
						{
							addedComparedItems.Add(comparedItem.addOwnedComparison(newConstraint, null));
						}
					}
				}
			}
			else
			{
				//no existing table, everything is new
				foreach (var newColumn in newTable.columns) 
				{
					addedComparedItems.Add(comparedItem.addOwnedComparison(newColumn,null));
				}
				foreach (var newConstraint in newTable.constraints) 
				{
					addedComparedItems.Add(comparedItem.addOwnedComparison(newConstraint,null));
				}
			}
			return addedComparedItems;
		}
		public DB.Database newDatabase {
			get { return _newDatabase;}
			set {this._newDatabase = (Database)value;}
		}

		public DB.Database existingDatabase {
			get { return _existingDatabase;}
			set {this._existingDatabase = (Database)value;}
		}

		public List<DatabaseItemComparison> comparedItems { get;set;}

		#endregion
	}
}
