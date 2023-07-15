using LuaDecompilerCore.CFG;

namespace LuaDecompilerCore.IR;

public interface IIrVisitor
{
    public void VisitFunction(Function function);
    public void VisitBasicBlock(BasicBlock basicBlock);
    public void VisitExpression(Expression expression);
    public void VisitConstant(Constant constant);
    public void VisitClosure(Closure closure);
    public void VisitIdentifier(Identifier identifier);
    public void VisitIdentifierReference(IdentifierReference identifierReference);
    public void VisitConcat(Concat concat);
    public void VisitInitializerList(InitializerList initializerList);
    public void VisitBinOp(BinOp binOp);
    public void VisitUnaryOp(UnaryOp unaryOp);
    public void VisitFunctionCall(FunctionCall functionCall);
    public void VisitAssignment(Assignment assignment);
    public void VisitBreak(Break @break);
    public void VisitContinue(Continue @continue);
    public void VisitData(Data data);
    public void VisitGenericFor(GenericFor genericFor);
    public void VisitIfStatement(IfStatement ifStatement);
    public void VisitJump(Jump jump);
    public void VisitLabel(Label label);
    public void VisitNumericFor(NumericFor numericFor);
    public void VisitPhiFunction(PhiFunction phiFunction);
    public void VisitPlaceholderInstruction(PlaceholderInstruction placeholderInstruction);
    public void VisitReturn(Return @return);
    public void VisitWhile(While @while);
}