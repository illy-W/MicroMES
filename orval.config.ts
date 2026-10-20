// orval.config.ts
import { defineConfig } from 'orval';

export default defineConfig({
  api: {
    input: './openapi/Api.json',
    output: {
      mode: 'tags',
      target: './libs/shared/api-client/src/generated/services',
      schemas: './libs/shared/api-client/src/generated/model',
      client: 'angular',
      formatter: 'prettier',
      clean: true,
      namingConvention: 'kebab-case',
      tsconfig: {
        compilerOptions: {
          target: 'es2020'
        }
      }
    }
  }
});
