const { RuleTester } = require('eslint');
const rule = require('../no-comments');

const ruleTester = new RuleTester({
  languageOptions: { ecmaVersion: 2022, sourceType: 'module' }
});

ruleTester.run('no-comments', rule, {
  valid: [
    { code: 'const a = 1;' },
    { code: 'export function f() { return 1; }' },
    { code: '#!/usr/bin/env node\nconst a = 1;' }
  ],
  invalid: [
    { code: 'const a = 1; // note', errors: [{ messageId: 'noComments' }] },
    { code: '/* block */ const a = 1;', errors: [{ messageId: 'noComments' }] },
    {
      code: '/** doc */\nconst a = 1; // two',
      errors: [{ messageId: 'noComments' }, { messageId: 'noComments' }]
    }
  ]
});
