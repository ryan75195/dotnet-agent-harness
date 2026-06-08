const { RuleTester } = require('eslint');
const rule = require('../one-component-per-file');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    parserOptions: { ecmaFeatures: { jsx: true } }
  }
});

ruleTester.run('one-component-per-file', rule, {
  valid: [
    { code: 'export function Foo() { return null; }' },
    { code: 'export function Foo() { return null; }\nfunction helper() { return 1; }' },
    { code: 'export default function App() { return null; }' },
    { code: 'export const useThing = () => 1;\nexport function Foo() { return null; }' }
  ],
  invalid: [
    {
      code: 'export function Foo() { return null; }\nexport function Bar() { return null; }',
      errors: [{ messageId: 'tooMany' }]
    },
    {
      code: 'export const Foo = () => null;\nexport const Bar = () => null;',
      errors: [{ messageId: 'tooMany' }]
    },
    {
      code: 'export default function App() { return null; }\nexport function Other() { return null; }',
      errors: [{ messageId: 'tooMany' }]
    }
  ]
});
