module.exports = {
  meta: {
    type: 'problem',
    schema: [],
    messages: {
      noComments: 'Comments are banned — extract intent into function, variable, or type names (see CLAUDE.md).'
    }
  },
  create(context) {
    return {
      Program() {
        for (const comment of context.sourceCode.getAllComments()) {
          if (comment.type === 'Shebang') {
            continue;
          }
          context.report({ loc: comment.loc, messageId: 'noComments' });
        }
      }
    };
  }
};
