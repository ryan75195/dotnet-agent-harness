function exportedName(declaration) {
  if (!declaration) {
    return null;
  }
  if (declaration.type === 'FunctionDeclaration' && declaration.id) {
    return declaration.id.name;
  }
  if (declaration.type === 'VariableDeclaration') {
    const declarator = declaration.declarations[0];
    const isFunction =
      declarator &&
      declarator.id.type === 'Identifier' &&
      declarator.init &&
      ['ArrowFunctionExpression', 'FunctionExpression'].includes(declarator.init.type);
    return isFunction ? declarator.id.name : null;
  }
  return null;
}

module.exports = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      tooMany: 'Only one exported component per file — move {{name}} to its own file.'
    }
  },
  create(context) {
    const components = [];
    function record(node) {
      const name = exportedName(node.declaration);
      if (name && /^[A-Z]/.test(name)) {
        components.push({ name, node });
      }
    }
    return {
      ExportNamedDeclaration: record,
      ExportDefaultDeclaration: record,
      'Program:exit'() {
        for (const extra of components.slice(1)) {
          context.report({ node: extra.node, messageId: 'tooMany', data: { name: extra.name } });
        }
      }
    };
  }
};
