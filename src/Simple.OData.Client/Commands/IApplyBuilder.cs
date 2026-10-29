namespace Simple.OData.Client.Commands;

/// <summary>Produces the text of an <c>$apply</c> query option for a resolved command.</summary>
internal interface IApplyBuilder
{
	/// <summary>Builds the <c>$apply</c> expression (unescaped) for <paramref name="command"/>.</summary>
	string Build(ResolvedCommand command, Session session);
}
