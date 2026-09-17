# TableMint

TableMint lets people define the structure of a data collection and then manage entries that conform to that structure.

## Language

**Table**:
A user-defined data collection that owns an ordered set of Field Definitions and its Records.
_Avoid_: Sheet, form, database

**Table Schema**:
The current ordered set of active Field Definitions that determines which values a Table's Records may contain.
_Avoid_: Database schema, physical table definition

**Schema Version**:
A monotonically increasing identifier for a Table Schema that changes whenever Record interpretation or validation changes.
_Avoid_: Record version, application version

**Field Definition**:
A named part of a Table's structure that defines one value's meaning and accepted shape. A Field Definition belongs to exactly one Table.
_Avoid_: Column, property

**Field Key**:
The immutable, human-readable identity of a Field Definition used to address its value within Record data.
_Avoid_: Field label, column name

**Record**:
One entry in a Table, containing values addressed by that Table's Field Definitions.
_Avoid_: Row, submission

**Select Option**:
An allowed value for a SingleSelect Field Definition, identified by an immutable value and presented with a changeable label.
_Avoid_: Choice text, enum member

**Schema Suggestion**:
An optional proposal of Field Definitions inferred from field names and sample values. It never changes a Table until a user accepts it.
_Avoid_: Automatic schema, AI-generated table

**Tenant**:
An organization whose Tables and Records are isolated from every other organization's data.
_Avoid_: User, account
