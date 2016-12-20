<template>
  <tr >
    <td>{{time | moment}}</td>
    <td>{{bid}}</td>
    <td>{{ask}}</td>
  </tr>
</template>

<script>
export default {
  props: ['time', 'bid', 'ask']
}
</script>
